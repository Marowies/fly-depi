using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkyScan.Core.Entities;
using SkyScan.Infrastructure.Data.Data_Sources;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkyScan.Core.Entities;
using SkyScan.Core.Entities.AirLine;
using SkyScan.Infrastructure.Data.Data_Sources;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

    namespace SkyScan.Presentation.Controllers
    {
        public class BookingController : Controller
        {
            private readonly SkyScanDbContext _dbContext;
            private readonly UserManager<User> _userManager;
            private const string GuestBookingsCookieName = "SkyScan_GuestBookings";

            public BookingController(SkyScanDbContext dbContext, UserManager<User> userManager)
            {
                _dbContext = dbContext;
                _userManager = userManager;
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
            public async Task<IActionResult> Book(string flightNumber, string departureTime, string origin, string destination)
            {
                if (!DateTime.TryParse(departureTime, out var depTime))
                {
                    return BadRequest("Invalid departure date.");
                }

                // Find the flight in our database with details
                var flight = await _dbContext.Flights
                    .Include(f => f.Airline)
                    .Include(f => f.DepartureAirport).ThenInclude(a => a.City)
                    .Include(f => f.ArrivalAirport).ThenInclude(a => a.City)
                    .FirstOrDefaultAsync(f => f.FlightNumber == flightNumber && f.DepartureTime == depTime);

                if (flight == null)
                {
                    return NotFound("Selected flight could not be found.");
                }

                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    // Create DB Booking for logged-in user
                    var booking = new Booking
                    {
                        BookingId = Guid.NewGuid(),
                        UserId = user.Id,
                        FlightId = flight.FlightId,
                        BookingDate = DateTime.UtcNow
                    };

                    _dbContext.Bookings.Add(booking);
                    await _dbContext.SaveChangesAsync();
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
                        AirlineName = flight.Airline?.Name ?? "Airlines",
                        RedirectUrl = flight.RedirectURL ?? $"https://www.google.com/travel/flights?q=Flights%20to%20{destination}%20from%20{origin}%20on%20{depTime:yyyy-MM-dd}"
                    });

                    SaveGuestBookingsToCookie(guestBookings);
                }

                // Redirect user to the flight redirect URL (Google Flights link)
                var redirectUrl = flight.RedirectURL;
                if (string.IsNullOrEmpty(redirectUrl))
                {
                    redirectUrl = $"https://www.google.com/travel/flights?q=Flights%20to%20{destination}%20from%20{origin}%20on%20{depTime:yyyy-MM-dd}";
                }

                return Redirect(redirectUrl);
            }

            [HttpGet]
            public async Task<IActionResult> MyBookings()
            {
                var user = await _userManager.GetUserAsync(User);
                var bookings = new List<Booking>();

                if (user != null)
                {
                    // Retrieve from database
                    bookings = await _dbContext.Bookings
                        .Include(b => b.Flight)
                            .ThenInclude(f => f.Airline)
                        .Include(b => b.Flight)
                            .ThenInclude(f => f.DepartureAirport)
                                .ThenInclude(a => a.City)
                        .Include(b => b.Flight)
                            .ThenInclude(f => f.ArrivalAirport)
                                .ThenInclude(a => a.City)
                        .Include(b => b.Flight)
                            .ThenInclude(f => f.Tickets)
                        .Where(b => b.UserId == user.Id)
                        .OrderByDescending(b => b.BookingDate)
                        .ToListAsync();
                }
                else
                {
                    // Guest user - Retrieve from cookies and map to Booking model objects for view compatibility
                    var guestBookings = GetGuestBookingsFromCookie();
                    foreach (var gb in guestBookings)
                    {
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

