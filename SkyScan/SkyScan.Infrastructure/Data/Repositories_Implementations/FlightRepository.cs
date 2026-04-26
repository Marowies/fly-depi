using Microsoft.EntityFrameworkCore;
using SkyScan.Core.Entities.AirLine;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Infrastructure.Data.Data_Sources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SkyScan.Infrastructure.Data.Repositories_Implementations
{
    public class FlightRepository : GenericRepository<Flight>, IFlightRepository
    {
        public FlightRepository(SkyScanDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Flight>> SearchFlightsAsync(Guid originCityId, Guid destinationCityId, DateTime departureDate)
        {
            return await _dbSet
                .Include(f => f.Airline)
                .Include(f => f.Airplane)
                .Include(f => f.DepartureAirport).ThenInclude(a => a.City)
                .Include(f => f.ArrivalAirport).ThenInclude(a => a.City)
                .Include(f => f.Tickets)
                .Where(f => f.DepartureAirport.CityId == originCityId 
                         && f.ArrivalAirport.CityId == destinationCityId 
                         && f.DepartureTime.Date == departureDate.Date)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<IEnumerable<Flight>> GetLowestPriceFlightsAsync(int count = 5)
        {
            return await _dbSet
                .Include(f => f.Airline)
                .Include(f => f.DepartureAirport).ThenInclude(a => a.City)
                .Include(f => f.ArrivalAirport).ThenInclude(a => a.City)
                .Include(f => f.Tickets)
                .OrderBy(f => f.Tickets.Min(t => t.Price))
                .Take(count)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<IEnumerable<Flight>> GetFlightsAroundTheWorldAsync(int count = 5)
        {
            return await _dbSet
                .Include(f => f.Airline)
                .Include(f => f.DepartureAirport).ThenInclude(a => a.City)
                .Include(f => f.ArrivalAirport).ThenInclude(a => a.City)
                .OrderBy(x => Guid.NewGuid()) // Random selection
                .Take(count)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
