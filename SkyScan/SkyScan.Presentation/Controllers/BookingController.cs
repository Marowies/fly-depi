using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkyScan.Core.Entities;
using SkyScan.Infrastructure.Data.Data_Sources;
using System;
using System.Threading.Tasks;

namespace SkyScan.Presentation.Controllers
{
    [Authorize]
    public class BookingController : Controller
    {
        private readonly SkyScanDbContext _dbContext;
        private readonly UserManager<User> _userManager;

        public BookingController(SkyScanDbContext dbContext, UserManager<User> userManager)
        {
            _dbContext = dbContext;
            _userManager = userManager;
        }

        [HttpPost]
        public async Task<IActionResult> Book(string flightNumber, string departureTime, string origin, string destination)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (!DateTime.TryParse(departureTime, out var depTime))
            {
                return BadRequest("Invalid departure date.");
            }

            // Find the flight in our database (it should be there because we persist Amadeus results)
            var flight = await _dbContext.Flights
                .FirstOrDefaultAsync(f => f.FlightNumber == flightNumber && f.DepartureTime == depTime);

            if (flight == null)
            {
                return NotFound("Selected flight could not be found.");
            }

            // Create Booking
            var booking = new Booking
            {
                BookingId = Guid.NewGuid(),
                UserId = user.Id,
                FlightId = flight.FlightId,
                BookingDate = DateTime.UtcNow
            };

            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

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
            if (user == null)
            {
                return Challenge();
            }

            var bookings = await _dbContext.Bookings
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

            return View(bookings);
        }
    }
}
