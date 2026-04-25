using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;
using SkyScan.Application.Interfaces;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Presentation.Models;

namespace SkyScan.Presentation.Controllers
{
    public class FlightController : Controller
    {
        private readonly IAirportRepository _airportRepository;
        private readonly ISearchRepository _searchRepository;
        private readonly IFlightProviderService _flightProviderService;
        private readonly IMemoryCache _cache;

        // Airport dropdown is static reference data — cache for 6 hours
        private const string AirportCacheKey = "airports_dropdown";
        private static readonly TimeSpan AirportCacheDuration = TimeSpan.FromHours(6);

        public FlightController(
            IAirportRepository airportRepository,
            IFlightProviderService flightProviderService,
            IMemoryCache cache)
        {
            _airportRepository = airportRepository;
            _flightProviderService = flightProviderService;
            _cache = cache;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var viewModel = new FlightSearchViewModel
            {
                CitiesWithAirports = await GetCachedAirportDropdownAsync()
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Search(FlightSearchViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.CitiesWithAirports = await GetCachedAirportDropdownAsync();
                return View("Index", model);
            }

            return RedirectToAction("Results", new
            {
                origin      = model.OriginCity,
                destination = model.DestinationCity,
                date        = model.DepartureDate.ToString("yyyy-MM-dd")
            });
        }

        [HttpGet]
        public async Task<IActionResult> Results(string origin, string destination, string date)
        {
            if (!DateTime.TryParse(date, out DateTime departureDate))
            {
                return RedirectToAction("Index");
            }

            // Run DB lookups sequentially to avoid DbContext concurrency issues
            // (DbContext cannot execute multiple queries at the exact same time on the same instance)
            var originAirport = await _airportRepository.GetByIataAsync(origin);
            var destAirport   = await _airportRepository.GetByIataAsync(destination);
            
            // API call can run independently
            var flightsTask = await _flightProviderService.SearchFlightsAsync(origin, destination, departureDate);

            var viewModel = new FlightResultsViewModel
            {
                OriginIata      = origin,
                DestinationIata = destination,
                OriginCity      = originAirport?.City?.Name ?? origin,
                DestinationCity = destAirport?.City?.Name ?? destination,
                DepartureDate   = departureDate,
                Flights         = flightsTask
            };

            return View(viewModel);
        }

        // --- Private Helpers ---

        /// <summary>
        /// Returns the airport dropdown list from cache, or fetches a projected query from DB on first call.
        /// The query only fetches 3 columns (IataCode, AirportName, CityName) — no full entity hydration.
        /// </summary>
        private async Task<List<SelectListItem>> GetCachedAirportDropdownAsync()
        {
            if (!_cache.TryGetValue(AirportCacheKey, out List<SelectListItem>? cachedItems) || cachedItems == null)
            {
                var cities = await _airportRepository.GetCityDropdownItemsAsync();

                cachedItems = cities.Select(c => new SelectListItem
                {
                    Value = c.CityId.ToString(),
                    Text  = $"{c.CityName}"
                }).OrderBy(c => c.Text).ToList();

                _cache.Set(AirportCacheKey, cachedItems, AirportCacheDuration);
            }

            return cachedItems;
        }
    }
}
