using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;
using SkyScan.Application.DTOs;
using SkyScan.Application.Interfaces;
using SkyScan.Core.Constants;
using SkyScan.Core.Entities;
using SkyScan.Core.Entities.AirLine;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Presentation.Models;


using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using SkyScan.Infrastructure.Data.Data_Sources;
using Microsoft.EntityFrameworkCore;

namespace SkyScan.Presentation.Controllers
{
    public class FlightController : Controller
    {
        private readonly IAirportRepository _airportRepository;
        private readonly ISearchRepository _searchRepository;
        private readonly IFlightRepository _flightRepository;
        private readonly IFlightProviderService _flightProviderService;
        private readonly IMemoryCache _cache;
        private readonly SkyScanDbContext _context;
        private readonly UserManager<User> _userManager;

        // Airport dropdown is static reference data — cache for 6 hours
        private const string AirportCacheKey = "airports_dropdown";
        private static readonly TimeSpan AirportCacheDuration = TimeSpan.FromHours(6);

        public FlightController(
            IAirportRepository airportRepository,
            IFlightRepository flightRepository,
            IFlightProviderService flightProviderService,
            IMemoryCache cache,
            SkyScanDbContext context,
            UserManager<User> userManager)
        {
            _flightRepository = flightRepository;
            _airportRepository = airportRepository;
            _flightProviderService = flightProviderService;
            _cache = cache;
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var viewModel = new FlightSearchViewModel
            {
                CitiesWithAirports = await GetCachedAirportDropdownAsync()
            };

            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var trending = await _context.Searches
                .Where(s => s.TimeStamp >= thirtyDaysAgo)
                .GroupBy(s => new { s.OriginCityId, s.DestinationCityId })
                .Select(g => new
                {
                    OriginId = g.Key.OriginCityId,
                    DestId = g.Key.DestinationCityId,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToListAsync();

            var trendingRoutesList = new List<TrendingRouteViewModel>();
            foreach (var route in trending)
            {
                var originCity = await _context.Cities.FindAsync(route.OriginId);
                var destCity = await _context.Cities.FindAsync(route.DestId);
                if (originCity != null && destCity != null)
                {
                    trendingRoutesList.Add(new TrendingRouteViewModel
                    {
                        OriginCityId = route.OriginId,
                        DestinationCityId = route.DestId,
                        OriginCityName = originCity.Name,
                        DestinationCityName = destCity.Name,
                        SearchCount = route.Count,
                        MinPrice = 150 + new Random().Next(50, 400)
                    });
                }
            }

            if (trendingRoutesList.Count < 4)
            {
                var dbCities = await _context.Cities.Take(10).ToListAsync();
                if (dbCities.Count >= 2)
                {
                    var defaults = new[]
                    {
                        new { OriginName = "Sydney", DestName = "Bangkok", Price = 450.0 },
                        new { OriginName = "Tokyo", DestName = "Singapore", Price = 620.0 },
                        new { OriginName = "London", DestName = "Dubai", Price = 590.0 },
                        new { OriginName = "Paris", DestName = "New York", Price = 780.0 }
                    };

                    foreach (var def in defaults)
                    {
                        var origin = dbCities.FirstOrDefault(c => c.Name.Contains(def.OriginName, StringComparison.OrdinalIgnoreCase)) ?? dbCities[0];
                        var dest = dbCities.FirstOrDefault(c => c.Name.Contains(def.DestName, StringComparison.OrdinalIgnoreCase)) ?? dbCities[Math.Min(1, dbCities.Count - 1)];

                        if (origin.CityId != dest.CityId && !trendingRoutesList.Any(r => r.OriginCityId == origin.CityId && r.DestinationCityId == dest.CityId))
                        {
                            trendingRoutesList.Add(new TrendingRouteViewModel
                            {
                                OriginCityId = origin.CityId,
                                DestinationCityId = dest.CityId,
                                OriginCityName = origin.Name,
                                DestinationCityName = dest.Name,
                                SearchCount = 12 + new Random().Next(1, 40),
                                MinPrice = def.Price
                            });
                        }
                    }
                }
            }

            viewModel.TrendingRoutes = trendingRoutesList.Take(5).ToList();

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

            // Log Search to database for trending/popular analytics
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    var tripTypeVal = Enum.TryParse(tripType, out TripType parsedType) ? parsedType : TripType.OneWay;

                    // Upsert: update existing route or insert new one
                    var existing = await _context.Searches
                        .FirstOrDefaultAsync(s => s.UserId == user.Id
                                               && s.OriginCityId == originId
                                               && s.DestinationCityId == destId);

                    if (existing != null)
                    {
                        // Update existing route
                        existing.TimeStamp = DateTime.UtcNow;
                        existing.DepartureDate = departureDate;
                        existing.Type = tripTypeVal;
                    }
                    else
                    {
                        // Insert new route
                        var searchLog = new Search
                        {
                            TimeStamp = DateTime.UtcNow,
                            Type = tripTypeVal,
                            DepartureDate = departureDate,
                            OriginCityId = originId,
                            DestinationCityId = destId,
                            UserId = user.Id
                        };
                        _context.Searches.Add(searchLog);

                        // Enforce max 5 unique routes per user — remove oldest by TimeStamp
                        var userSearches = await _context.Searches
                            .Where(s => s.UserId == user.Id)
                            .OrderByDescending(s => s.TimeStamp)
                            .ToListAsync();

                        if (userSearches.Count >= 5)
                        {
                            var toDelete = userSearches.Skip(4);
                            _context.Searches.RemoveRange(toDelete);
                        }
                    }

                    // Increment destination city search count for popularity tracking
                    var destCity1 = await _context.Cities.FindAsync(destId);
                    if (destCity1 != null)
                    {
                        destCity1.SearchCount++;
                    }

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error logging search: {ex.Message}");
            }

            // Resolve City Names and all Airports for the search (City-to-City support)
            var originAirports = await _airportRepository.GetAirportsByCityIdAsync(originId);
            var destAirports = await _airportRepository.GetAirportsByCityIdAsync(destId);

            var originIatas = originAirports.Select(a => a.IataCode).Where(i => !string.IsNullOrEmpty(i)).ToList();
            var destIatas = destAirports.Select(a => a.IataCode).Where(i => !string.IsNullOrEmpty(i)).ToList();

            // Resolve City Names from the first available airport or default
            var originCity = originAirports.FirstOrDefault()?.City;
            var originName = originCity != null 
                ? $"{originCity.Name}, {originCity.Country?.Name ?? originCity.CountryCode}" 
                : "Origin";

            var destCity = destAirports.FirstOrDefault()?.City;
            var destName = destCity != null 
                ? $"{destCity.Name}, {destCity.Country?.Name ?? destCity.CountryCode}" 
                : "Destination";
            
            // Search Flights via the provider (Mock or Real) with Caching
            var cacheKey = $"flights_{string.Join("-", originIatas)}_{string.Join("-", destIatas)}_{departureDate:yyyyMMdd}";
            if (!_cache.TryGetValue(cacheKey, out List<FlightDto>? flightsList) || flightsList == null)
            {
                var freshFlights = await _flightProviderService.SearchFlightsAsync(originIatas!, destIatas!, departureDate);
                flightsList = freshFlights.ToList();
                _cache.Set(cacheKey, flightsList, TimeSpan.FromMinutes(15));
            }

            var viewModel = new FlightResultsViewModel
            {
                OriginIata      = string.Join("/", originIatas),
                DestinationIata = string.Join("/", destIatas),
                OriginCity      = originName,
                DestinationCity = destName,
                DepartureDate   = departureDate,
                Flights         = flightsList
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetNearestCity(double lat, double lon)
        {
            var city = await _airportRepository.GetNearestCityByCoordinatesAsync(lat, lon);
            if (city == null)
            {
                return NotFound("No nearby city found.");
            }
            return Json(new { cityId = city.CityId, name = city.Name });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ToggleFavorite(string flightNumber, string departureTime, decimal price)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (!DateTime.TryParse(departureTime, out var depTime)) return BadRequest("Invalid date format");

            // Find the flight in our database
            var dbFlight = await _context.Flights.FirstOrDefaultAsync(f => f.FlightNumber == flightNumber && f.DepartureTime == depTime);
            if (dbFlight == null) return NotFound("Flight not found in database");

            // Find or Create Trip for this flight
            var trip = await _context.Trips
                .Include(t => t.Flights)
                .FirstOrDefaultAsync(t => t.Flights.Any(f => f.FlightId == dbFlight.FlightId));

            if (trip == null)
            {
                trip = new Trip
                {
                    TripId = Guid.NewGuid(),
                    TotalPrice = (double)price,
                    Flights = new List<Flight> { dbFlight }
                };
                _context.Trips.Add(trip);
                await _context.SaveChangesAsync();
            }

            // Check if price alert / favorite already exists
            var alert = await _context.PriceAlerts
                .FirstOrDefaultAsync(pa => pa.UserId == user.Id && pa.TripId == trip.TripId);

            if (alert != null)
            {
                _context.PriceAlerts.Remove(alert);
                await _context.SaveChangesAsync();
                return Json(new { favorited = false });
            }
            else
            {
                alert = new PriceAlert
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    TripId = trip.TripId,
                    TargetPrice = price
                };
                _context.PriceAlerts.Add(alert);
                await _context.SaveChangesAsync();
                return Json(new { favorited = true });
            }
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
