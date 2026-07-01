using Microsoft.EntityFrameworkCore;
using SkyScan.Core.Entities;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Infrastructure.Data.Data_Sources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SkyScan.Infrastructure.Data.Repositories_Implementations
{
    public class AirportRepository : GenericRepository<Airport>, IAirportRepository
    {
        public AirportRepository(SkyScanDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Airport>> GetAllWithDetailsAsync()
        {
            return await _dbSet
                .Include(a => a.City)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Airport?> GetByIataAsync(string iataCode)
        {
            return await _dbSet
                .Include(a => a.City)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.IataCode == iataCode);
        }

        public async Task<IEnumerable<(Guid CityId, string CityName, string CountryName)>> GetCityDropdownItemsAsync()
        {
            var cities = await _context.Cities
                .Where(c => c.Airports.Any())
                .Select(c => new { c.CityId, c.Name, CountryName = c.Country != null ? c.Country.Name : c.CountryCode })
                .AsNoTracking()
                .ToListAsync();

            return cities.Select(c => (c.CityId, c.Name, c.CountryName));
        }

        public async Task<IEnumerable<Airport>> GetAirportsByCityIdAsync(Guid cityId)
        {
            return await _dbSet
                .Include(a => a.City)
                    .ThenInclude(c => c.Country)
                .Where(a => a.CityId == cityId)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<City?> GetNearestCityByCoordinatesAsync(double latitude, double longitude)
        {
            // Latitude and Longitude have been removed from the database schema.
            return await Task.FromResult<City?>(null);
        }
    }
}
