using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SkyScan.Core.Entities;
using SkyScan.Core.Entities.AirLine;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Infrastructure.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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
        private readonly IConfiguration _configuration;
        private const string GuestBookingsCookieName = "SkyScan_GuestBookings";

        public BookingController(
            IFlightRepository flightRepository, 
            IBookingRepository bookingRepository, 
            UserManager<ApplicationUser> userManager,
            IPriceAlertRepository priceAlertRepository,
            IConfiguration configuration)
        {
            _flightRepository = flightRepository;
            _bookingRepository = bookingRepository;
            _userManager = userManager;
            _priceAlertRepository = priceAlertRepository;
            _configuration = configuration;
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
            bool returnHasEntertainment = false,
            bool createPriceAlert = false,
            bool addToCalendar = false)
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

            var mainBookingId = Guid.NewGuid();
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                await _bookingRepository.AddAsync(new Booking
                {
                    BookingId = mainBookingId,
                    UserId = user.Id,
                    FlightId = flight.FlightId,
                    BookingDate = DateTime.UtcNow
                });

                if (createPriceAlert)
                {
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

                    if (createPriceAlert)
                    {
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
            }
            else
            {
                // Guest Booking - save to cookie
                var guestBookings = GetGuestBookingsFromCookie();

                guestBookings.Add(new GuestBookingCookieModel
                {
                    BookingId = mainBookingId,
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

            if (addToCalendar)
            {
                return RedirectToAction(nameof(AddToGoogleCalendar), new { bookingId = mainBookingId, isBookingFlow = true });
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

        [HttpGet]
        public async Task<IActionResult> AddToGoogleCalendar(Guid bookingId, bool isBookingFlow = false)
        {
            var booking = await FindBookingAsync(bookingId);
            if (booking == null)
            {
                TempData["Error"] = "Booking not found.";
                return RedirectToAction(nameof(MyBookings));
            }

            var googleClientId = _configuration["Authentication:Google:ClientId"];
            if (string.IsNullOrEmpty(googleClientId))
            {
                TempData["Error"] = "Google Calendar integration is not configured.";
                if (isBookingFlow)
                {
                    return Redirect(GetFlightRedirectUrl(booking));
                }
                return RedirectToAction(nameof(MyBookings));
            }

            var redirectUri = Url.Action("GoogleCalendarCallback", "Booking", null, Request.Scheme);
            
            var state = isBookingFlow ? $"{bookingId}|book" : bookingId.ToString();

            var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth" +
                          $"?client_id={googleClientId}" +
                          $"&redirect_uri={Uri.EscapeDataString(redirectUri!)}" +
                          $"&response_type=code" +
                          $"&scope={Uri.EscapeDataString("https://www.googleapis.com/auth/calendar.events")}" +
                          $"&state={state}" +
                          $"&access_type=online" +
                          $"&prompt=consent";

            return Redirect(authUrl);
        }

        [HttpGet]
        public async Task<IActionResult> GoogleCalendarCallback(string? code, string? state, string? error)
        {
            if (string.IsNullOrEmpty(state))
            {
                TempData["Error"] = "Invalid response from Google authorization server (missing state).";
                return RedirectToAction(nameof(MyBookings));
            }

            var stateParts = state.Split('|');
            if (!Guid.TryParse(stateParts[0], out var bookingId))
            {
                TempData["Error"] = "Invalid booking state.";
                return RedirectToAction(nameof(MyBookings));
            }

            var isBookingFlow = stateParts.Length > 1 && stateParts[1] == "book";

            if (!string.IsNullOrEmpty(error))
            {
                TempData["Error"] = $"Google Calendar authorization failed: {error}";
                if (isBookingFlow)
                {
                    var fallbackBooking = await FindBookingAsync(bookingId);
                    if (fallbackBooking != null) return Redirect(GetFlightRedirectUrl(fallbackBooking));
                }
                return RedirectToAction(nameof(MyBookings));
            }

            if (string.IsNullOrEmpty(code))
            {
                TempData["Error"] = "Invalid response from Google authorization server (missing code).";
                if (isBookingFlow)
                {
                    var fallbackBooking = await FindBookingAsync(bookingId);
                    if (fallbackBooking != null) return Redirect(GetFlightRedirectUrl(fallbackBooking));
                }
                return RedirectToAction(nameof(MyBookings));
            }

            var booking = await FindBookingAsync(bookingId);
            if (booking == null)
            {
                TempData["Error"] = "Booking associated with this calendar event was not found.";
                return RedirectToAction(nameof(MyBookings));
            }

            var googleClientId = _configuration["Authentication:Google:ClientId"];
            var googleClientSecret = _configuration["Authentication:Google:ClientSecret"];
            var redirectUri = Url.Action("GoogleCalendarCallback", "Booking", null, Request.Scheme);

            using var client = new HttpClient();
            string accessToken;

            try
            {
                var tokenRequestParams = new Dictionary<string, string>
                {
                    { "client_id", googleClientId ?? "" },
                    { "client_secret", googleClientSecret ?? "" },
                    { "code", code },
                    { "grant_type", "authorization_code" },
                    { "redirect_uri", redirectUri ?? "" }
                };

                var tokenResponse = await client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(tokenRequestParams));
                if (!tokenResponse.IsSuccessStatusCode)
                {
                    var errorBody = await tokenResponse.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Failed to retrieve access token from Google: {errorBody}";
                    if (isBookingFlow) return Redirect(GetFlightRedirectUrl(booking));
                    return RedirectToAction(nameof(MyBookings));
                }

                var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
                using var tokenDoc = JsonDocument.Parse(tokenJson);
                accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString() ?? "";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to exchange authorization code: {ex.Message}";
                if (isBookingFlow) return Redirect(GetFlightRedirectUrl(booking));
                return RedirectToAction(nameof(MyBookings));
            }

            if (string.IsNullOrEmpty(accessToken))
            {
                TempData["Error"] = "Retrieved an empty access token from Google.";
                if (isBookingFlow) return Redirect(GetFlightRedirectUrl(booking));
                return RedirectToAction(nameof(MyBookings));
            }

            try
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var flight = booking.Flight;
                var origin = flight.DepartureAirport?.City?.Name ?? flight.DepartureAirport?.IataCode ?? "Origin";
                var destination = flight.ArrivalAirport?.City?.Name ?? flight.ArrivalAirport?.IataCode ?? "Destination";
                
                var title = $"Flight {flight.FlightNumber} — {origin} to {destination}";
                var location = $"{flight.DepartureAirport?.Name} ({flight.DepartureAirport?.IataCode})";
                var description = $"Flight details for your upcoming trip:\n\n" +
                                  $"• Flight: {flight.FlightNumber}\n" +
                                  $"• Airline: {flight.Airline?.Name ?? "N/A"}\n" +
                                  $"• Route: {origin} ({flight.DepartureAirport?.IataCode}) → {destination} ({flight.ArrivalAirport?.IataCode})\n" +
                                  $"• Departure: {flight.DepartureTime:dddd, MMMM dd, yyyy} at {flight.DepartureTime:HH:mm} (Local Time)\n" +
                                  $"• Arrival: {flight.ArrivalTime:dddd, MMMM dd, yyyy} at {flight.ArrivalTime:HH:mm} (Local Time)\n" +
                                  $"• Booking Reference: {booking.BookingId}\n" +
                                  $"• Manage Booking / Check-in: {flight.RedirectURL}";

                var eventPayload = new
                {
                    summary = title,
                    location = location,
                    description = description,
                    start = new { dateTime = flight.DepartureTime.ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "UTC" },
                    end = new { dateTime = flight.ArrivalTime.ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "UTC" },
                    reminders = new
                    {
                        useDefault = false,
                        overrides = new[]
                        {
                            new { method = "email", minutes = 1440 }, // 1 day before
                            new { method = "popup", minutes = 180 }   // 3 hours before
                        }
                    }
                };

                var payloadJson = JsonSerializer.Serialize(eventPayload);
                var content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json");

                var calendarResponse = await client.PostAsync("https://www.googleapis.com/calendar/v3/calendars/primary/events", content);
                if (calendarResponse.IsSuccessStatusCode)
                {
                    TempData["Message"] = "Flight successfully added to your Google Calendar with reminders!";
                }
                else
                {
                    var calendarError = await calendarResponse.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Failed to create Google Calendar event: {calendarError}";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error calling Google Calendar API: {ex.Message}";
            }

            if (isBookingFlow)
            {
                return Redirect(GetFlightRedirectUrl(booking));
            }

            return RedirectToAction(nameof(MyBookings));
        }

        private string GetFlightRedirectUrl(Booking booking)
        {
            var flight = booking.Flight;
            var finalRedirectUrl = flight.Airline?.Url;
            if (string.IsNullOrEmpty(finalRedirectUrl))
            {
                finalRedirectUrl = flight.RedirectURL;
            }
            if (string.IsNullOrEmpty(finalRedirectUrl))
            {
                var queryStr = $"flights from {flight.DepartureAirport?.IataCode} to {flight.ArrivalAirport?.IataCode} on {flight.DepartureTime:yyyy-MM-dd}";
                finalRedirectUrl = $"https://www.google.com/travel/flights?q={Uri.EscapeDataString(queryStr)}";
            }
            return finalRedirectUrl;
        }

        private async Task<Booking?> FindBookingAsync(Guid bookingId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var bookings = await _bookingRepository.GetBookingsByUserIdAsync(user.Id);
                return bookings.FirstOrDefault(b => b.BookingId == bookingId);
            }
            else
            {
                var guestBookings = GetGuestBookingsFromCookie();
                var gb = guestBookings.FirstOrDefault(b => b.BookingId == bookingId);
                if (gb == null) return null;

                return new Booking
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
                };
            }
        }
    }
}
