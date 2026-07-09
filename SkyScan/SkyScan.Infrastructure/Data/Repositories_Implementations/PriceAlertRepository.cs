using Microsoft.EntityFrameworkCore;
using SkyScan.Core.Entities;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Infrastructure.Data.Data_Sources;
using SkyScan.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SkyScan.Core.Entities.AirLine;

namespace SkyScan.Infrastructure.Data.Repositories_Implementations
{
    public class PriceAlertRepository : GenericRepository<PriceAlert>, IPriceAlertRepository
    {
        public PriceAlertRepository(SkyScanDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<PriceAlert>> GetPriceAlertsByUserIdAsync(Guid userId)
        {
            return await _dbSet
                .Include(a => a.Trip)
                    .ThenInclude(t => t.Flights)
                        .ThenInclude(f => f.Airline)
                .Include(a => a.Trip)
                    .ThenInclude(t => t.Flights)
                        .ThenInclude(f => f.DepartureAirport)
                            .ThenInclude(ap => ap.City)
                .Include(a => a.Trip)
                    .ThenInclude(t => t.Flights)
                        .ThenInclude(f => f.ArrivalAirport)
                            .ThenInclude(ap => ap.City)
                .Where(a => a.UserId == userId)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<PriceAlert?> FindByUserAndTripAsync(Guid userId, Guid tripId)
        {
            return await _dbSet.FirstOrDefaultAsync(a => a.UserId == userId && a.TripId == tripId);
        }

        public async Task<IEnumerable<PriceAlert>> GetAllWithDetailsAsync()
        {
            // No User include here — PriceAlert (Core) doesn't hold a nav to the
            // Infrastructure-owned ApplicationUser. Callers resolve the user separately
            // (e.g. via UserManager) by alert.UserId when they need contact details.
            return await _dbSet
                .Include(a => a.Trip)
                    .ThenInclude(t => t.Flights)
                        .ThenInclude(f => f.Airline)
                .Include(a => a.Trip)
                    .ThenInclude(t => t.Flights)
                        .ThenInclude(f => f.DepartureAirport)
                .Include(a => a.Trip)
                    .ThenInclude(t => t.Flights)
                        .ThenInclude(f => f.ArrivalAirport)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Trip> EnsureTripExistsForFlightAsync(Guid flightId, decimal price)
        {
            var trip = await _context.Trips
                .Include(t => t.Flights)
                .FirstOrDefaultAsync(t => t.Flights.Any(f => f.FlightId == flightId));

            if (trip == null)
            {
                var flight = await _context.Flights.FindAsync(flightId);
                trip = new Trip
                {
                    TripId = Guid.NewGuid(),
                    TotalPrice = (double)price,
                    Flights = flight != null ? new List<Flight> { flight } : new List<Flight>()
                };
                _context.Trips.Add(trip);
                await _context.SaveChangesAsync();
            }

            return trip;
        }
        public async Task DeleteUserAlertsAsync(Guid userId)
        {
            var alerts = await _dbSet.Where(a => a.UserId == userId).ToListAsync();
            if (alerts.Any())
            {
                _dbSet.RemoveRange(alerts);
                await _context.SaveChangesAsync();
            }
        }
    }
}
