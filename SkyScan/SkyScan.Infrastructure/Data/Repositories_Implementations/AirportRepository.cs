using Microsoft.EntityFrameworkCore;
using SkyScan.Core.Entities;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Infrastructure.Data.Data_Sources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;

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
                .FirstOrDefaultAsync(a => a.IataCode == iataCode);
        }

        public async Task<IEnumerable<(Guid CityId, string CityName, string CountryName, string? CityNameAr, string? CountryNameAr)>> GetCityDropdownItemsAsync()
        {
            var airports = await _dbSet
                .Include(a => a.City)
                .ThenInclude(c => c.Country)
                .AsNoTracking()
                .ToListAsync();

            return airports
                .Where(a => a.City != null)
                .Select(a => (
                    CityId: a.CityId, 
                    CityName: a.City.Name, 
                    CountryName: a.City.Country?.Name ?? "",
                    CityNameAr: a.City.NameAr,
                    CountryNameAr: a.City.Country?.NameAr
                ))
                .Distinct()
                .OrderBy(c => c.CityName);
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

        public async Task<City?> GetCityByNameAsync(string cityName)
        {
            if (string.IsNullOrEmpty(cityName)) return null;

            var lowerName = cityName.ToLower();

            // First, exact match
            var city = await _context.Cities
                .Include(c => c.Country)
                .FirstOrDefaultAsync(c => c.Name.ToLower() == lowerName);
            if (city != null) return city;

            // Second, partial/contains match
            city = await _context.Cities
                .Include(c => c.Country)
                .FirstOrDefaultAsync(c => lowerName.Contains(c.Name.ToLower()) || c.Name.ToLower().Contains(lowerName));
            
            return city;
        }

        public async Task<City?> GetCityByIdAsync(Guid cityId)
        {
            return await _context.Cities
                .Include(c => c.Country)
                .FirstOrDefaultAsync(c => c.CityId == cityId);
        }

        public async Task IncrementCitySearchCountAsync(Guid cityId)
        {
            var city = await _context.Cities.FindAsync(cityId);
            if (city == null) return;

            city.SearchCount++;
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<City>> GetTopCitiesBySearchCountAsync(int count = 20)
        {
            return await _context.Cities
                .OrderByDescending(c => c.SearchCount)
                .Take(count)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
