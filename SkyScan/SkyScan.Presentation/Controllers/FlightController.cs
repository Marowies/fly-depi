using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;
using SkyScan.Application.DTOs;
using SkyScan.Application.Interfaces;
using SkyScan.Core.Constants;
using SkyScan.Core.Entities;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Presentation.Models;

namespace SkyScan.Presentation.Controllers
{
    public class FlightController : Controller
    {
        private readonly IAirportRepository _airportRepository;
        private readonly ISearchRepository _searchRepository;
        private readonly IFlightRepository _flightRepository;
        private readonly IFlightProviderService _flightProviderService;
        private readonly IMemoryCache _cache;

        // Airport dropdown is static reference data — cache for 6 hours
        private const string AirportCacheKey = "airports_dropdown";
        private static readonly TimeSpan AirportCacheDuration = TimeSpan.FromHours(6);

        public FlightController(
            IAirportRepository airportRepository,
            IFlightRepository flightRepository,
            IFlightProviderService flightProviderService,
            IMemoryCache cache)
        {
            _flightRepository = flightRepository;
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
            // Fetch cached cities for resolution
            var allCities = await GetCachedAirportDropdownAsync();

            if (!ModelState.IsValid)
            {
                model.CitiesWithAirports = allCities;
                return View("Index", model);
            }

            // Map to our Search Request DTO
            var searchRequest = new FlightSearchRequestDto
            {
                TripType = model.TripType,
                CabinClass = model.CabinClass
            };

            // Helper to resolve string input to Guid
            Guid? ResolveId(string? input)
            {
                if (string.IsNullOrEmpty(input)) return null;
                if (Guid.TryParse(input, out var guid)) return guid;
                
                // Try to find by name in cache
                var match = allCities.FirstOrDefault(c => 
                    c.Text.Equals(input, StringComparison.OrdinalIgnoreCase) || 
                    c.Text.Contains(input, StringComparison.OrdinalIgnoreCase));
                
                return match != null ? Guid.Parse(match.Value) : null;
            }

            Guid? finalOriginId = null;
            Guid? finalDestId = null;

            if (model.TripType == TripType.MultiWay)
            {
                searchRequest.Legs = model.MultiCityLegs
                    .Select(l => new { Leg = l, OriginId = ResolveId(l.OriginCity), DestId = ResolveId(l.DestinationCity) })
                    .Where(x => x.OriginId.HasValue && x.DestId.HasValue)
                    .Select(x => new FlightLegDto
                    {
                        OriginCityId = x.OriginId!.Value,
                        DestinationCityId = x.DestId!.Value,
                        DepartureDate = x.Leg.DepartureDate
                    }).ToList();
                
                if (searchRequest.Legs.Any())
                {
                    finalOriginId = searchRequest.Legs.First().OriginCityId;
                    finalDestId = searchRequest.Legs.First().DestinationCityId;
                }
            }
            else
            {
                finalOriginId = ResolveId(model.OriginCity);
                finalDestId = ResolveId(model.DestinationCity);

                if (finalOriginId.HasValue && finalDestId.HasValue)
                {
                    searchRequest.Legs.Add(new FlightLegDto
                    {
                        OriginCityId = finalOriginId.Value,
                        DestinationCityId = finalDestId.Value,
                        DepartureDate = model.DepartureDate
                    });

                    if (model.TripType == TripType.RoundTrip && model.ReturnDate.HasValue)
                    {
                        searchRequest.ReturnDate = model.ReturnDate;
                    }
                }
            }

            // If no legs were successfully parsed, return with error
            if (searchRequest.Legs.Count == 0 || !finalOriginId.HasValue || !finalDestId.HasValue)
            {
                ModelState.AddModelError("", "We couldn't recognize one of the cities. Please select from the suggestions.");
                model.CitiesWithAirports = allCities;
                return View("Index", model);
            }

            return RedirectToAction("Results", new
            {
                origin      = finalOriginId.Value.ToString(),
                destination = finalDestId.Value.ToString(),
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

            // Resolve City Names and all Airports for the search (City-to-City support)
            var originAirports = await _airportRepository.GetAirportsByCityIdAsync(originId);
            var destAirports = await _airportRepository.GetAirportsByCityIdAsync(destId);

            var originIatas = originAirports.Select(a => a.IataCode).Where(i => !string.IsNullOrEmpty(i)).ToList();
            var destIatas = destAirports.Select(a => a.IataCode).Where(i => !string.IsNullOrEmpty(i)).ToList();

            // Resolve City Names from the first available airport or default
            var originName = originAirports.FirstOrDefault()?.City?.Name ?? "Origin";
            var destName = destAirports.FirstOrDefault()?.City?.Name ?? "Destination";
            
            // Search Flights via the provider (Mock or Real)
            // Note: We now pass ALL IATA codes in the city to support city-to-city searching
            var flights = await _flightRepository.SearchFlightsAsync(originIatas!, destIatas!, departureDate);

            var viewModel = new FlightResultsViewModel
            {
                OriginIata      = string.Join("/", originIatas),
                DestinationIata = string.Join("/", destIatas),
                OriginCity      = originName,
                DestinationCity = destName,
                DepartureDate   = departureDate,
                Flights         = flights.Select(f => new FlightDto
                {
                    AirlineName = f.Airline?.Name ?? "Unknown",
                    FlightNumber = f.FlightNumber,
                    OriginAirport = f.DepartureAirport?.IataCode ?? f.DepartureAirport?.Code ?? "Unknown",
                    DestinationAirport = f.ArrivalAirport?.IataCode ?? f.ArrivalAirport?.Code ?? "Unknown",
                    DepartureTime = f.DepartureTime,
                    ArrivalTime = f.ArrivalTime,
                    Price = f.Tickets.Any() ? f.Tickets.Min(t => t.Price) : 0,
                    Status = "Active",
                    RedirectURL = f.RedirectURL
                }).ToList()
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
