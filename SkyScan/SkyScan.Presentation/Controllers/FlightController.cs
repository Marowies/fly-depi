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
using SkyScan.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;

namespace SkyScan.Presentation.Controllers
{
    public class FlightController : Controller
    {
        private readonly IAirportRepository _airportRepository;
        private readonly ISearchRepository _searchRepository;
        private readonly IFlightRepository _flightRepository;
        private readonly IPriceAlertRepository _priceAlertRepository;
        private readonly IGenericRepository<Airline> _airlineRepository;
        private readonly IGenericRepository<Airplane> _airplaneRepository;
        private readonly IFlightProviderService _flightProviderService;
        private readonly ILocationLookupService _locationLookupService;
        private readonly IMemoryCache _cache;
        private readonly UserManager<ApplicationUser> _userManager;

        // Airport dropdown is static reference data — cache for 6 hours
        private const string AirportCacheKey = "airports_dropdown";
        private static readonly TimeSpan AirportCacheDuration = TimeSpan.FromHours(6);
        private static readonly TimeSpan SearchCacheDuration = TimeSpan.FromMinutes(15);
        private const string UnknownAircraftCode = "UNK";

        private readonly SkyScan.Presentation.Services.ILanguageService _languageService;

        public FlightController(
            IAirportRepository airportRepository,
            ISearchRepository searchRepository,
            IFlightRepository flightRepository,
            IPriceAlertRepository priceAlertRepository,
            IGenericRepository<Airline> airlineRepository,
            IGenericRepository<Airplane> airplaneRepository,
            IFlightProviderService flightProviderService,
            ILocationLookupService locationLookupService,
            IMemoryCache cache,
            UserManager<ApplicationUser> userManager,
            SkyScan.Presentation.Services.ILanguageService languageService)
        {
            _flightRepository = flightRepository;
            _airportRepository = airportRepository;
            _searchRepository = searchRepository;
            _priceAlertRepository = priceAlertRepository;
            _airlineRepository = airlineRepository;
            _airplaneRepository = airplaneRepository;
            _flightProviderService = flightProviderService;
            _locationLookupService = locationLookupService;
            _cache = cache;
            _userManager = userManager;
            _languageService = languageService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var rawCities = await GetCachedAirportDropdownAsync();
            var currentLang = _languageService.CurrentLanguage;

            var viewModel = new FlightSearchViewModel
            {
                CitiesWithAirports = rawCities.Select(c => new SelectListItem
                {
                    Value = c.Value,
                    Text  = c.Text
                }).OrderBy(c => c.Text).ToList()
            };

            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var trending = await _searchRepository.GetTrendingRoutesSinceAsync(thirtyDaysAgo, 5);

            // GetTrendingRoutesSinceAsync already returns one Search per distinct route (top 5 by count)
            var trendingRoutesList = trending
                .Where(s => s.OriginCity != null && s.DestinationCity != null)
                .Select(s => new TrendingRouteViewModel
                {
                    OriginCityId = s.OriginCityId,
                    DestinationCityId = s.DestinationCityId,
                    OriginCityName = s.OriginCity.Name,
                    DestinationCityName = s.DestinationCity.Name,
                    SearchCount = s.OriginCity.SearchCount + s.DestinationCity.SearchCount,
                    MinPrice = 150 + new Random().Next(50, 400)
                })
                .ToList();

            // If we don't have 5 routes from searches, fallback to pairing database cities to guarantee exactly 5 routes
            if (trendingRoutesList.Count < 5)
            {
                var dbCities = (await _airportRepository.GetTopCitiesBySearchCountAsync(20)).ToList();
                if (dbCities.Count >= 2)
                {
                    for (int i = 0; i < dbCities.Count - 1 && trendingRoutesList.Count < 5; i++)
                    {
                        var origin = dbCities[i];
                        var dest = dbCities[i + 1];
                        if (origin.CityId != dest.CityId && !trendingRoutesList.Any(r => r.OriginCityId == origin.CityId && r.DestinationCityId == dest.CityId))
                        {
                            trendingRoutesList.Add(new TrendingRouteViewModel
                            {
                                OriginCityId = origin.CityId,
                                DestinationCityId = dest.CityId,
                                OriginCityName = origin.Name,
                                DestinationCityName = dest.Name,
                                SearchCount = origin.SearchCount + dest.SearchCount,
                                MinPrice = 190 + new Random().Next(40, 450)
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
                
                // Try to find by name in cache (checks English text, English City part)
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
                tripType    = model.TripType.ToString(),
                returnDate  = searchRequest.ReturnDate?.ToString("yyyy-MM-dd")
            });
        }

        [HttpGet]
        [EnableRateLimiting("SearchPolicy")]
        public async Task<IActionResult> Results(string origin, string destination, string date, string tripType = "OneWay", string? returnDate = null)
        {
            if (!DateTime.TryParse(date, out DateTime departureDate))
            {
                return RedirectToAction("Index");
            }

            if (departureDate.Date < DateTime.Today)
            {
                var newDepartureDate = DateTime.Today.AddDays(1);
                var newReturnDate = returnDate;

                if (!string.IsNullOrEmpty(returnDate) && DateTime.TryParse(returnDate, out DateTime parsedReturnDate))
                {
                    var gap = parsedReturnDate.Date - departureDate.Date;
                    if (gap < TimeSpan.Zero) gap = TimeSpan.FromDays(7);
                    newReturnDate = newDepartureDate.Add(gap).ToString("yyyy-MM-dd");
                }

                return RedirectToAction(nameof(Results), new
                {
                    origin = origin,
                    destination = destination,
                    date = newDepartureDate.ToString("yyyy-MM-dd"),
                    tripType = tripType,
                    returnDate = newReturnDate
                });
            }

            // Backend Validation: Verify that the submitted IDs are valid GUIDs and exist in the DB
            if (!Guid.TryParse(origin, out Guid originId) || !Guid.TryParse(destination, out Guid destId))
            {
                Console.WriteLine($"Results Debug: Parsing failed. Origin: '{origin}', Destination: '{destination}'");
                return RedirectToAction("Index");
            }

            DateTime returnDepartureDate = DateTime.Now.AddDays(7);

            var tripTypeVal = Enum.TryParse(tripType, out TripType parsedTripType) ? parsedTripType : TripType.OneWay;
            var isRoundTrip = tripTypeVal == TripType.RoundTrip && DateTime.TryParse(returnDate, out  returnDepartureDate);

            // Log Search to database for trending/popular analytics
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    await _searchRepository.LogSearchAsync(user.Id, originId, destId, departureDate, tripTypeVal);
                    await _airportRepository.IncrementCitySearchCountAsync(destId);
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
            var currentLang = _languageService.CurrentLanguage;
            var originCity = originAirports.FirstOrDefault()?.City;
            var originName = originCity != null 
                ? $"{originCity.Name}, {originCity.Country?.Name ?? originCity.CountryCode}" 
                : "Origin";

            var destCity = destAirports.FirstOrDefault()?.City;
            var destName = destCity != null 
                ? $"{destCity.Name}, {destCity.Country?.Name ?? destCity.CountryCode}" 
                : "Destination";
            
            // Search Flights via the provider (Mock or Real) with Caching
            var viewModel = new FlightResultsViewModel
            {
                OriginIata      = string.Join("/", originIatas),
                DestinationIata = string.Join("/", destIatas),
                OriginCity      = originName,
                DestinationCity = destName,
                DepartureDate   = departureDate,
                IsRoundTrip     = isRoundTrip
            };

            if (isRoundTrip)
            {
                viewModel.ReturnDate = returnDepartureDate;
                viewModel.Flights = await SearchLegAsync(originIatas!, destIatas!, departureDate, returnDepartureDate);
            }
            else
            {
                viewModel.Flights = await SearchLegAsync(originIatas!, destIatas!, departureDate);
            }

            return View(viewModel);
        }

        /// <summary>
        /// Searches one direction of a journey (a set of origin airports to a set of destination
        /// airports on a given date), transparently caching the provider response for 15 minutes.
        /// </summary>
        private async Task<List<FlightDto>> SearchLegAsync(IEnumerable<string> originIatas, IEnumerable<string> destIatas, DateTime date, DateTime? returnDate = null)
        {
            var cacheKey = $"flights_{string.Join("-", originIatas)}_{string.Join("-", destIatas)}_{date:yyyyMMdd}" + (returnDate.HasValue ? $"_{returnDate.Value:yyyyMMdd}" : "");
            if (_cache.TryGetValue(cacheKey, out List<FlightDto>? cached) && cached != null)
            {
                return cached;
            }

            var freshFlights = await _flightProviderService.SearchFlightsAsync(originIatas, destIatas, date, returnDate);
            var flights = freshFlights.ToList();
            _cache.Set(cacheKey, flights, SearchCacheDuration);
            return flights;
        }

        [HttpGet]
        public async Task<IActionResult> GetNearestCity(double lat, double lon)
        {
            var city = await _locationLookupService.GetNearestCityAsync(lat, lon);
            if (city == null)
            {
                return NotFound("No nearby city found.");
            }

            var dbCity = await _airportRepository.GetCityByIdAsync(city.CityId);
            if (dbCity == null)
            {
                return Json(new { cityId = city.CityId, name = city.Name });
            }

            var currentLang = _languageService.CurrentLanguage;
            var isAr = currentLang == "ar";

            var cityName = isAr && !string.IsNullOrEmpty(dbCity.NameAr) ? dbCity.NameAr : dbCity.Name;
            var countryName = isAr && !string.IsNullOrEmpty(dbCity.Country?.NameAr) ? dbCity.Country.NameAr : dbCity.Country?.Name ?? dbCity.CountryCode;

            var displayName = $"{cityName}, {countryName}";

            return Json(new { cityId = dbCity.CityId, name = displayName });
        }

        [HttpPost]
        [Authorize]
        [EnableRateLimiting("BookingPolicy")]
        public async Task<IActionResult> ToggleFavorite(ToggleFavoriteRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (!DateTime.TryParse(request.DepartureTime, out var depTime))
                return BadRequest("Invalid date format");

            DateTime.TryParse(request.ArrivalTime, out var arrTime);

            var flight = await _flightRepository.EnsureFlightExistsAsync(
                request.FlightNumber,
                depTime,
                request.OriginIata,
                request.DestinationIata,
                request.AirlineName,
                arrTime == default ? depTime : arrTime,
                request.RedirectUrl ?? string.Empty
            );

            if (flight == null)
                return BadRequest("Could not resolve this flight's airports.");

            var trip = await _priceAlertRepository.EnsureTripExistsForFlightAsync(flight.FlightId, request.Price);

            var existing = await _priceAlertRepository.FindByUserAndTripAsync(user.Id, trip.TripId);
            if (existing != null)
            {
                await _priceAlertRepository.DeleteAsync(existing);
                return Json(new { favorited = false });
            }

            await _priceAlertRepository.AddAsync(new PriceAlert
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TripId = trip.TripId,
                TargetPrice = request.Price
            });
            return Json(new { favorited = true });
        }

        // --- Private Helpers ---

        /// <summary>
        /// Returns the airport dropdown list from cache, or fetches a projected query from DB on first call.
        /// The query only fetches 3 columns (IataCode, AirportName, CityName) — no full entity hydration.
        /// </summary>
        private async Task<List<SelectListItem>> GetCachedAirportDropdownAsync()
        {
            var currentLang = _languageService.CurrentLanguage;
            var cacheKey = $"{AirportCacheKey}_{currentLang}";
            if (!_cache.TryGetValue(cacheKey, out List<SelectListItem>? cachedItems) || cachedItems == null)
            {
                var cities = await _airportRepository.GetCityDropdownItemsAsync();
                var isAr = currentLang == "ar";

                cachedItems = cities.Select(c => 
                {
                    var cityName = isAr && !string.IsNullOrEmpty(c.CityNameAr) ? c.CityNameAr : c.CityName;
                    var countryName = isAr && !string.IsNullOrEmpty(c.CountryNameAr) ? c.CountryNameAr : c.CountryName;
                    return new SelectListItem
                    {
                        Value = c.CityId.ToString(),
                        Text  = $"{cityName}, {countryName}"
                    };
                }).OrderBy(c => c.Text).ToList();

                _cache.Set(cacheKey, cachedItems, AirportCacheDuration);
            }

            return cachedItems;
        }
    }
}
