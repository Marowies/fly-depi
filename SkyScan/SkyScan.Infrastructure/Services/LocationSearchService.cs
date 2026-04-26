using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkyScan.Application.DTOs;
using SkyScan.Application.Interfaces;
using SkyScan.Infrastructure.Data.Data_Sources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SkyScan.Infrastructure.Services
{
    public class LocationSearchService : ILocationSearchService
    {
        private List<CitySuggestionDto> _searchIndex = new();
        private readonly IServiceProvider _serviceProvider;
        private bool _isInitialized = false;

        public LocationSearchService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<SkyScanDbContext>();
                
                // Increase timeout just in case
                context.Database.SetCommandTimeout(120);

                // Fetch data separately and join in memory to avoid heavy SQL group-by performance issues
                var citiesRaw = await context.Cities
                    .Select(c => new { c.CityId, c.Name, c.CountryCode })
                    .AsNoTracking()
                    .ToListAsync();

                var airportRanks = await context.Airports
                    .Select(a => new { a.CityId, a.Type })
                    .AsNoTracking()
                    .ToListAsync();

                // Group airport types by CityId in memory
                var cityAirportTypes = airportRanks
                    .GroupBy(a => a.CityId)
                    .ToDictionary(
                        g => g.Key, 
                        g => g.Select(a => a.Type).ToList()
                    );

                var cities = citiesRaw.Select(c => {
                    var types = cityAirportTypes.ContainsKey(c.CityId) ? cityAirportTypes[c.CityId] : new List<string?>();
                    
                    int rank = 4; // Default: No airports
                    if (types.Any(t => t == "large_airport")) rank = 1;
                    else if (types.Any(t => t == "medium_airport")) rank = 2;
                    else if (types.Any()) rank = 3;

                    return new CitySuggestionDto
                    {
                        CityId = c.CityId,
                        DisplayName = c.Name,
                        CountryCode = c.CountryCode,
                        Rank = rank
                    };
                })
                .Where(c => c.Rank < 4) // Only include cities with at least one airport
                .OrderBy(c => c.Rank)
                .ThenBy(c => c.DisplayName)
                .ToList();

                _searchIndex = cities;
                _isInitialized = true;
            }
        }

        public IEnumerable<CitySuggestionDto> Search(string query, int limit = 10)
        {
            if (string.IsNullOrWhiteSpace(query) || !_isInitialized) 
                return Enumerable.Empty<CitySuggestionDto>();

            // Fast prefix matching in memory
            return _searchIndex
                .Where(c => c.DisplayName.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                .Take(limit);
        }
    }
}
