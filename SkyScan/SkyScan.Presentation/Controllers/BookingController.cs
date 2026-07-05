using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SkyScan.Core.Entities;
using SkyScan.Core.Entities.AirLine;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Infrastructure.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SkyScan.Presentation.Controllers
{
    public class BookingController : Controller
    {
        private readonly IFlightRepository _flightRepository;
        private readonly IBookingRepository _bookingRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPriceAlertRepository _priceAlertRepository;
        private const string GuestBookingsCookieName = "SkyScan_GuestBookings";

        public BookingController(
            IFlightRepository flightRepository, 
            IBookingRepository bookingRepository, 
            UserManager<ApplicationUser> userManager,
            IPriceAlertRepository priceAlertRepository)
        {
            _flightRepository = flightRepository;
            _bookingRepository = bookingRepository;
            _userManager = userManager;
            _priceAlertRepository = priceAlertRepository;
        }

        // Helper class to serialize guest bookings in cookie
        public class GuestBookingCookieModel
        {
            public Guid BookingId { get; set; }
            public DateTime BookingDate { get; set; }
            public string FlightNumber { get; set; }
            public DateTime DepartureTime { get; set; }
            public DateTime ArrivalTime { get; set; }
            public string OriginCityName { get; set; }
            public string OriginIata { get; set; }
            public string DestinationCityName { get; set; }
            public string DestinationIata { get; set; }
            public string AirlineName { get; set; }
            public string RedirectUrl { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Book(
            string flightNumber,
            string departureTime,
            string origin,
            string destination,
            string originIata,
            string destinationIata,
            string airlineName,
            string arrivalTime,
            decimal price,
            string redirectUrl,
            bool hasWifi = false,
            bool hasFood = false,
            bool hasEntertainment = false,
            string? returnFlightNumber = null,
            string? returnDepartureTime = null,
            string? returnOrigin = null,
            string? returnDestination = null,
            string? returnOriginIata = null,
            string? returnDestinationIata = null,
            string? returnAirlineName = null,
            string? returnArrivalTime = null,
            bool returnHasWifi = false,
            bool returnHasFood = false,
            bool returnHasEntertainment = false)
        {
            if (!DateTime.TryParse(departureTime, out var depTime))
            {
                return BadRequest("Invalid departure date.");
            }

            var flight = await _flightRepository.GetByFlightNumberAndDepartureAsync(flightNumber, depTime);
            if (flight == null)
            {
                // Materialize flight on demand
                DateTime.TryParse(arrivalTime, out var arrTime);
                flight = await _flightRepository.EnsureFlightExistsAsync(
                    flightNumber,
                    depTime,
                    originIata,
                    destinationIata,
                    airlineName,
                    arrTime == default ? depTime : arrTime,
                    redirectUrl,
                    price,
                    hasWifi,
                    hasFood,
                    hasEntertainment
                );
            }

            if (flight == null)
            {
                return NotFound("Selected outbound flight could not be found or materialized.");
            }

            Flight? returnFlight = null;
            if (!string.IsNullOrEmpty(returnFlightNumber) && DateTime.TryParse(returnDepartureTime, out var retDepTime))
            {
                returnFlight = await _flightRepository.GetByFlightNumberAndDepartureAsync(returnFlightNumber, retDepTime);
                if (returnFlight == null)
                {
                    DateTime.TryParse(returnArrivalTime, out var retArrTime);
                    returnFlight = await _flightRepository.EnsureFlightExistsAsync(
                        returnFlightNumber,
                        retDepTime,
                        returnOriginIata!,
                        returnDestinationIata!,
                        returnAirlineName!,
                        retArrTime == default ? retDepTime : retArrTime,
                        redirectUrl,
                        0.00M, // return price is included in outbound package price
                        returnHasWifi,
                        returnHasFood,
                        returnHasEntertainment
                    );
                }
            }

            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                await _bookingRepository.AddAsync(new Booking
                {
                    BookingId = Guid.NewGuid(),
                    UserId = user.Id,
                    FlightId = flight.FlightId,
                    BookingDate = DateTime.UtcNow
                });

                // Auto-Favorite Outbound Flight via interface
                var outboundTrip = await _priceAlertRepository.EnsureTripExistsForFlightAsync(flight.FlightId, price);
                var existingOutboundAlert = await _priceAlertRepository.FindByUserAndTripAsync(user.Id, outboundTrip.TripId);
                if (existingOutboundAlert == null)
                {
                    await _priceAlertRepository.AddAsync(new PriceAlert
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        TripId = outboundTrip.TripId,
                        TargetPrice = price
                    });
                }

                if (returnFlight != null)
                {
                    await _bookingRepository.AddAsync(new Booking
                    {
                        BookingId = Guid.NewGuid(),
                        UserId = user.Id,
                        FlightId = returnFlight.FlightId,
                        BookingDate = DateTime.UtcNow
                    });

                    // Auto-Favorite Return Flight via interface
                    var returnTrip = await _priceAlertRepository.EnsureTripExistsForFlightAsync(returnFlight.FlightId, 0.00M);
                    var existingReturnAlert = await _priceAlertRepository.FindByUserAndTripAsync(user.Id, returnTrip.TripId);
                    if (existingReturnAlert == null)
                    {
                        await _priceAlertRepository.AddAsync(new PriceAlert
                        {
                            Id = Guid.NewGuid(),
                            UserId = user.Id,
                            TripId = returnTrip.TripId,
                            TargetPrice = 0.00M
                        });
                    }
                }
            }
            else
            {
                // Guest Booking - save to cookie
                var guestBookings = GetGuestBookingsFromCookie();

                guestBookings.Add(new GuestBookingCookieModel
                {
                    BookingId = Guid.NewGuid(),
                    BookingDate = DateTime.UtcNow,
                    FlightNumber = flight.FlightNumber,
                    DepartureTime = flight.DepartureTime,
                    ArrivalTime = flight.ArrivalTime,
                    OriginCityName = flight.DepartureAirport?.City?.Name ?? origin,
                    OriginIata = flight.DepartureAirport?.IataCode ?? origin,
                    DestinationCityName = flight.ArrivalAirport?.City?.Name ?? destination,
                    DestinationIata = flight.ArrivalAirport?.IataCode ?? destination,
                    AirlineName = flight.Airline?.Name ?? airlineName,
                    RedirectUrl = flight.RedirectURL ?? redirectUrl
                });

                if (returnFlight != null)
                {
                    guestBookings.Add(new GuestBookingCookieModel
                    {
                        BookingId = Guid.NewGuid(),
                        BookingDate = DateTime.UtcNow,
                        FlightNumber = returnFlight.FlightNumber,
                        DepartureTime = returnFlight.DepartureTime,
                        ArrivalTime = returnFlight.ArrivalTime,
                        OriginCityName = returnFlight.DepartureAirport?.City?.Name ?? returnOrigin ?? destination,
                        OriginIata = returnFlight.DepartureAirport?.IataCode ?? returnOriginIata ?? destinationIata,
                        DestinationCityName = returnFlight.ArrivalAirport?.City?.Name ?? returnDestination ?? origin,
                        DestinationIata = returnFlight.ArrivalAirport?.IataCode ?? returnDestinationIata ?? originIata,
                        AirlineName = returnFlight.Airline?.Name ?? returnAirlineName ?? airlineName,
                        RedirectUrl = returnFlight.RedirectURL ?? redirectUrl
                    });
                }

                SaveGuestBookingsToCookie(guestBookings);
            }

            // Redirect user to the flight redirect URL (Airline official site or fallback)
            var finalRedirectUrl = flight.Airline?.Url;
            if (string.IsNullOrEmpty(finalRedirectUrl))
            {
                finalRedirectUrl = flight.RedirectURL;
            }
            if (string.IsNullOrEmpty(finalRedirectUrl))
            {
                var queryStr = $"flights from {originIata} to {destinationIata} on {depTime:yyyy-MM-dd}";
                if (!string.IsNullOrEmpty(returnDepartureTime) && DateTime.TryParse(returnDepartureTime, out var retDate))
                {
                    queryStr += $" through {retDate:yyyy-MM-dd}";
                }
                finalRedirectUrl = $"https://www.google.com/travel/flights?q={Uri.EscapeDataString(queryStr)}";
            }

            return Redirect(finalRedirectUrl);
        }

        [HttpGet]
        public async Task<IActionResult> MyBookings()
        {
            var user = await _userManager.GetUserAsync(User);
            var bookings = new List<Booking>();
            var now = DateTime.UtcNow;

            if (user != null)
            {
                var allBookings = (await _bookingRepository.GetBookingsByUserIdAsync(user.Id)).ToList();
                foreach (var b in allBookings)
                {
                    if (b.Flight != null && b.Flight.DepartureTime < now)
                    {
                        await _bookingRepository.DeleteAsync(b);
                    }
                    else
                    {
                        bookings.Add(b);
                    }
                }
            }
            else
            {
                // Guest user - Retrieve from cookies, filter out expired flights, and save
                var guestBookings = GetGuestBookingsFromCookie();
                var updatedGuests = new List<GuestBookingCookieModel>();
                foreach (var gb in guestBookings)
                {
                    if (gb.DepartureTime >= now)
                    {
                        updatedGuests.Add(gb);
                        bookings.Add(new Booking
                        {
                            BookingId = gb.BookingId,
                            BookingDate = gb.BookingDate,
                            Flight = new Flight
                            {
                                FlightNumber = gb.FlightNumber,
                                DepartureTime = gb.DepartureTime,
                                ArrivalTime = gb.ArrivalTime,
                                RedirectURL = gb.RedirectUrl,
                                Airline = new Airline { Name = gb.AirlineName },
                                DepartureAirport = new Airport
                                {
                                    IataCode = gb.OriginIata,
                                    City = new City { Name = gb.OriginCityName }
                                },
                                ArrivalAirport = new Airport
                                {
                                    IataCode = gb.DestinationIata,
                                    City = new City { Name = gb.DestinationCityName }
                                }
                            }
                        });
                    }
                }
                SaveGuestBookingsToCookie(updatedGuests);
            }

            return View(bookings);
        }

        private List<GuestBookingCookieModel> GetGuestBookingsFromCookie()
        {
            var cookie = Request.Cookies[GuestBookingsCookieName];
            if (string.IsNullOrEmpty(cookie))
            {
                return new List<GuestBookingCookieModel>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<GuestBookingCookieModel>>(cookie) ?? new List<GuestBookingCookieModel>();
            }
            catch
            {
                return new List<GuestBookingCookieModel>();
            }
        }

        private void SaveGuestBookingsToCookie(List<GuestBookingCookieModel> bookings)
        {
            var json = JsonSerializer.Serialize(bookings);
            Response.Cookies.Append(GuestBookingsCookieName, json, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                HttpOnly = true,
                Secure = true
            });
        }
    }
}
