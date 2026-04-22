using Microsoft.EntityFrameworkCore;
using SkyScan.Application.DTOs;
using SkyScan.Core.Entities;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Infrastructure.Data.Data_Sources;

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
        /// Projects only the 3 required columns — bypasses full entity graph hydration.
        public async Task<IEnumerable<AirportDropdownDto>> GetDropdownItemsAsync()
        {
            return await _dbSet
                .Where(a => a.IataCode != null && a.IataCode.Length > 0)
                .OrderBy(a => a.City.Name)
                .ThenBy(a => a.Name)
                .Select(a => new AirportDropdownDto
                {
                    IataCode    = a.IataCode,
                    AirportName = a.Name,
                    CityName    = a.City != null ? a.City.Name : string.Empty
                })
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
    }
}
