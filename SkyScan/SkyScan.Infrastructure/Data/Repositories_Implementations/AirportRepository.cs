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

        public async Task<City?> GetNearestCityByCoordinatesAsync(double latitude, double longitude)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("SkyScanApp/1.0 (contact: skyscanorg@gmail.com)");
                var url = $"https://nominatim.openstreetmap.org/reverse?format=json&lat={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&longitude={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&zoom=10&accept-language=en";
                
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("address", out var addressEl))
                    {
                        string? cityName = null;
                        if (addressEl.TryGetProperty("city", out var cityEl)) cityName = cityEl.GetString();
                        else if (addressEl.TryGetProperty("town", out var townEl)) cityName = townEl.GetString();
                        else if (addressEl.TryGetProperty("village", out var villageEl)) cityName = villageEl.GetString();
                        else if (addressEl.TryGetProperty("suburb", out var suburbEl)) cityName = suburbEl.GetString();
                        else if (addressEl.TryGetProperty("county", out var countyEl)) cityName = countyEl.GetString();
                        else if (addressEl.TryGetProperty("state", out var stateEl)) cityName = stateEl.GetString();

                        if (!string.IsNullOrEmpty(cityName))
                        {
                            var lowerName = cityName.ToLower();
                            // First, exact match
                            var city = await _context.Cities
                                .FirstOrDefaultAsync(c => c.Name.ToLower() == lowerName);
                            if (city != null) return city;

                            // Second, partial/contains match
                            city = await _context.Cities
                                .FirstOrDefaultAsync(c => lowerName.Contains(c.Name.ToLower()) || c.Name.ToLower().Contains(lowerName));
                            if (city != null) return city;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in local reverse geocoding: {ex.Message}");
            }
            return null;
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
