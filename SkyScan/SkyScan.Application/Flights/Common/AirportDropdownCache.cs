using Microsoft.Extensions.Caching.Memory;
using SkyScan.Application.Common.Interfaces;
using SkyScan.Core.Repositories_Interfaces;

namespace SkyScan.Application.Flights.Common
{
    public class CityDropdownItem
    {
        public Guid CityId { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>
    /// Relocated from FlightController.GetCachedAirportDropdownAsync (Phase 2c) — shared by
    /// GetHomeSearchDataQueryHandler, GetCityDropdownQueryHandler, and SearchFlightsQueryHandler
    /// (for city-name resolution) so the same cache entry and matching text ("City, Country")
    /// is used everywhere a free-text city needs to be resolved or displayed. Not itself a
    /// MediatR request — it's an internal helper injected into handlers that need it.
    /// </summary>
    public class AirportDropdownCache
    {
        private readonly IAirportRepository _airportRepository;
        private readonly IMemoryCache _cache;
        private readonly ICurrentLanguageProvider _languageProvider;
        private const string CacheKeyPrefix = "airports_dropdown";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

        public AirportDropdownCache(IAirportRepository airportRepository, IMemoryCache cache, ICurrentLanguageProvider languageProvider)
        {
            _airportRepository = airportRepository;
            _cache = cache;
            _languageProvider = languageProvider;
        }

        public async Task<List<CityDropdownItem>> GetAsync()
        {
            var currentLang = _languageProvider.CurrentLanguage;
            var cacheKey = $"{CacheKeyPrefix}_{currentLang}";

            if (!_cache.TryGetValue(cacheKey, out List<CityDropdownItem>? cachedItems) || cachedItems == null)
            {
                var cities = await _airportRepository.GetCityDropdownItemsAsync();
                var isAr = currentLang == "ar";

                cachedItems = cities.Select(c =>
                {
                    var cityName = isAr && !string.IsNullOrEmpty(c.CityNameAr) ? c.CityNameAr : c.CityName;
                    var countryName = isAr && !string.IsNullOrEmpty(c.CountryNameAr) ? c.CountryNameAr : c.CountryName;
                    return new CityDropdownItem
                    {
                        CityId = c.CityId,
                        Text = $"{cityName}, {countryName}"
                    };
                }).OrderBy(c => c.Text).ToList();

                _cache.Set(cacheKey, cachedItems, CacheDuration);
            }

            return cachedItems;
        }
    }
}
