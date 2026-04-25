using Microsoft.EntityFrameworkCore;
using SkyScan.Application.DTOs;
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

        /// <inheritdoc/>
        public async Task<IEnumerable<Airport>> GetAllWithDetailsAsync()
        {
            return await _dbSet
                .Include(a => a.City)
                .ThenInclude(c => c.Country)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <inheritdoc/>
        /// Uses the IataCode index for an O(log n) lookup — never loads the full table.
        public async Task<Airport?> GetByIataAsync(string iataCode)
        {
            return await _dbSet
                .Include(a => a.City)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.IataCode == iataCode);
        }

        public async Task<IEnumerable<(Guid CityId, string CityName)>> GetCityDropdownItemsAsync()
        {
            var uniqueCities = await _context.Cities
                .Where(c => c.Airports.Any())
                .Select(c => new { c.CityId, CityName = c.Name })
                .AsNoTracking()
                .ToListAsync();

            return uniqueCities.Select(c => (c.CityId, c.CityName));
        }
    }
}
