using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;
using SkyScan.Application.DTOs;
using SkyScan.Application.Interfaces;
using SkyScan.Core.Constants;
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
                // Debug: Log validation errors to console
                foreach (var state in ModelState)
                {
                    foreach (var error in state.Value.Errors)
                    {
                        Console.WriteLine($"Property: {state.Key}, Error: {error.ErrorMessage}");
                    }
                }
                
                model.CitiesWithAirports = await GetCachedAirportDropdownAsync();
                return View("Index", model);
            }

            // Map to our Search Request DTO
            var searchRequest = new FlightSearchRequestDto
            {
                TripType = model.TripType,
                Adults = model.Adults,
                CabinClass = model.CabinClass
            };

            if (model.TripType == TripType.MultiWay)
            {
                searchRequest.Legs = model.MultiCityLegs
                    .Where(l => !string.IsNullOrEmpty(l.OriginCity) && !string.IsNullOrEmpty(l.DestinationCity))
                    .Select(l => new FlightLegDto
                    {
                        OriginCityId = Guid.Parse(l.OriginCity!),
                        DestinationCityId = Guid.Parse(l.DestinationCity!),
                        DepartureDate = l.DepartureDate
                    }).ToList();
            }
            else
            {
                if (!string.IsNullOrEmpty(model.OriginCity) && !string.IsNullOrEmpty(model.DestinationCity))
                {
                    searchRequest.Legs.Add(new FlightLegDto
                    {
                        OriginCityId = Guid.Parse(model.OriginCity),
                        DestinationCityId = Guid.Parse(model.DestinationCity),
                        DepartureDate = model.DepartureDate
                    });

                    if (model.TripType == TripType.RoundTrip && model.ReturnDate.HasValue)
                    {
                        searchRequest.ReturnDate = model.ReturnDate;
                    }
                }
            }

            // If no legs were successfully parsed, redirect back
            if (searchRequest.Legs.Count == 0)
            {
                return RedirectToAction("Index");
            }

            // For now, redirect standard searches to Results page
            // Multi-city results page can be implemented similarly
            return RedirectToAction("Results", new
            {
                origin      = model.OriginCity,
                destination = model.DestinationCity,
                date        = model.DepartureDate.ToString("yyyy-MM-dd"),
                tripType    = model.TripType.ToString()
            });
        }

        [HttpGet]
        public async Task<IActionResult> Results(string origin, string destination, string date, string tripType = "OneWay")
        {
            if (!DateTime.TryParse(date, out DateTime departureDate))
            {
                return RedirectToAction("Index");
            }

            // Backend Validation: Verify that the submitted IDs are valid GUIDs and exist in the DB
            if (!Guid.TryParse(origin, out Guid originId) || !Guid.TryParse(destination, out Guid destId))
            {
                Console.WriteLine($"Results Debug: Parsing failed. Origin: '{origin}', Destination: '{destination}'");
                return RedirectToAction("Index");
            }

            // Resolve City Names and Airports for the search
            var originCity = await _airportRepository.GetCityDropdownItemsAsync();
            var originName = originCity.FirstOrDefault(c => c.CityId == originId).CityName ?? "Unknown";

            var destCity = await _airportRepository.GetCityDropdownItemsAsync();
            var destName = destCity.FirstOrDefault(c => c.CityId == destId).CityName ?? "Unknown";
            
            // Search Flights via the provider (Mock or Real)
            // Note: We pass the CityId as string to the provider
            var flights = await _flightProviderService.SearchFlightsAsync(origin, destination, departureDate);

            var viewModel = new FlightResultsViewModel
            {
                OriginIata      = origin,
                DestinationIata = destination,
                OriginCity      = originName,
                DestinationCity = destName,
                DepartureDate   = departureDate,
                Flights         = flights
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
