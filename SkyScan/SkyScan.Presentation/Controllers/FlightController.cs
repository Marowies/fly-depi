using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using SkyScan.Application.Flights.GetCityDropdown;
using SkyScan.Application.Flights.GetFlightResults;
using SkyScan.Application.Flights.GetHomeSearchData;
using SkyScan.Application.Flights.GetNearestCity;
using SkyScan.Application.Flights.SearchFlights;
using SkyScan.Application.Flights.ToggleFavorite;
using SkyScan.Infrastructure.Identity;
using SkyScan.Presentation.Models;

namespace SkyScan.Presentation.Controllers
{
    public class FlightController : Controller
    {
        private readonly IMediator _mediator;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<FlightController> _logger;

        public FlightController(IMediator mediator, UserManager<ApplicationUser> userManager, ILogger<FlightController> logger)
        {
            _mediator = mediator;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var data = await _mediator.Send(new GetHomeSearchDataQuery());

            var viewModel = new FlightSearchViewModel
            {
                CitiesWithAirports = ToSelectListItems(data.CitiesDropdown),
                TrendingRoutes = data.TrendingRoutes.Select(t => new TrendingRouteViewModel
                {
                    OriginCityId = t.OriginCityId,
                    DestinationCityId = t.DestinationCityId,
                    OriginCityName = t.OriginCityName,
                    DestinationCityName = t.DestinationCityName,
                    SearchCount = t.SearchCount
                }).ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Search(FlightSearchViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.CitiesWithAirports = await GetCitiesDropdownAsync();
                return View("Index", model);
            }

            var query = new SearchFlightsQuery
            {
                TripType = model.TripType,
                OriginCity = model.OriginCity,
                DestinationCity = model.DestinationCity,
                DepartureDate = model.DepartureDate,
                ReturnDate = model.ReturnDate,
                CabinClass = model.CabinClass,
                MultiCityLegs = model.MultiCityLegs.Select(l => new SearchFlightsLegInput
                {
                    OriginCity = l.OriginCity,
                    DestinationCity = l.DestinationCity,
                    DepartureDate = l.DepartureDate
                }).ToList()
            };

            var result = await _mediator.Send(query);

            if (!result.Success)
            {
                ModelState.AddModelError("", result.ErrorMessage ?? "We couldn't process this search.");
                model.CitiesWithAirports = await GetCitiesDropdownAsync();
                return View("Index", model);
            }

            return RedirectToAction("Results", new
            {
                origin      = result.OriginCityId.ToString(),
                destination = result.DestinationCityId.ToString(),
                date        = result.DepartureDate.ToString("yyyy-MM-dd"),
                tripType    = result.TripType.ToString(),
                returnDate  = result.ReturnDate?.ToString("yyyy-MM-dd")
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
                _logger.LogWarning("Results: Guid parsing failed. Origin: '{Origin}', Destination: '{Destination}'", origin, destination);
                return RedirectToAction("Index");
            }

            var tripTypeVal = Enum.TryParse(tripType, out SkyScan.Core.Constants.TripType parsedTripType) ? parsedTripType : SkyScan.Core.Constants.TripType.OneWay;
            DateTime? returnDepartureDate = DateTime.TryParse(returnDate, out var parsedReturn) ? parsedReturn : null;

            var user = await _userManager.GetUserAsync(User);

            var result = await _mediator.Send(new GetFlightResultsQuery
            {
                OriginCityId = originId,
                DestinationCityId = destId,
                DepartureDate = departureDate,
                TripType = tripTypeVal,
                ReturnDate = returnDepartureDate,
                CurrentUserId = user?.Id
            });

            var viewModel = new FlightResultsViewModel
            {
                OriginIata = result.OriginIata,
                DestinationIata = result.DestinationIata,
                OriginCity = result.OriginCity,
                DestinationCity = result.DestinationCity,
                DepartureDate = result.DepartureDate,
                IsRoundTrip = result.IsRoundTrip,
                ReturnDate = result.ReturnDate,
                Flights = result.Flights
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetNearestCity(double lat, double lon)
        {
            var result = await _mediator.Send(new GetNearestCityQuery { Lat = lat, Lon = lon });

            if (!result.Found)
            {
                return NotFound("No nearby city found.");
            }

            return Json(new { cityId = result.CityId, name = result.DisplayName });
        }

        [HttpPost]
        [Authorize]
        [EnableRateLimiting("BookingPolicy")]
        public async Task<IActionResult> ToggleFavorite(ToggleFavoriteRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var result = await _mediator.Send(new ToggleFavoriteCommand
            {
                UserId = user.Id,
                FlightNumber = request.FlightNumber,
                DepartureTime = request.DepartureTime,
                ArrivalTime = request.ArrivalTime,
                Price = request.Price,
                AirlineName = request.AirlineName,
                OriginIata = request.OriginIata,
                DestinationIata = request.DestinationIata,
                RedirectUrl = request.RedirectUrl
            });

            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }

            return Json(new { favorited = result.Favorited });
        }

        // --- Private Helpers ---

        private async Task<List<SelectListItem>> GetCitiesDropdownAsync()
        {
            var dropdown = await _mediator.Send(new GetCityDropdownQuery());
            return ToSelectListItems(dropdown);
        }

        private static List<SelectListItem> ToSelectListItems(List<SkyScan.Application.Flights.Common.CityDropdownItem> items) =>
            items.Select(c => new SelectListItem { Value = c.CityId.ToString(), Text = c.Text }).ToList();
    }
}
