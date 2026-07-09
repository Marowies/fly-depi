using FluentValidation;
using MediatR;
using SkyScan.Application.DTOs;
using SkyScan.Application.Flights.Common;
using SkyScan.Core.Constants;

namespace SkyScan.Application.Flights.SearchFlights
{
    public class SearchFlightsQueryHandler : IRequestHandler<SearchFlightsQuery, SearchFlightsResult>
    {
        private readonly AirportDropdownCache _dropdownCache;
        private readonly IValidator<FlightSearchRequestDto> _validator;

        public SearchFlightsQueryHandler(AirportDropdownCache dropdownCache, IValidator<FlightSearchRequestDto> validator)
        {
            _dropdownCache = dropdownCache;
            _validator = validator;
        }

        public async Task<SearchFlightsResult> Handle(SearchFlightsQuery request, CancellationToken cancellationToken)
        {
            var allCities = await _dropdownCache.GetAsync();

            // Relocated from FlightController.Search's local ResolveId function (Phase 0 Finding #8).
            // Deliberately kept as the exact same matching algorithm/data source as before (parse as
            // Guid first, else exact/contains match against this cached dropdown's "City, Country"
            // text) rather than routed through a separate prefix-only autocomplete index over a
            // possibly different city set, which risked silently changing which searches resolve.
            // See Phase 2c report Flag F2. (That separate index -- ILocationSearchService -- was
            // itself confirmed dead and removed in Phase 2d; see Phase 2d report §4.)
            Guid? ResolveId(string? input)
            {
                if (string.IsNullOrEmpty(input)) return null;
                if (Guid.TryParse(input, out var guid)) return guid;

                var match = allCities.FirstOrDefault(c =>
                    c.Text.Equals(input, StringComparison.OrdinalIgnoreCase) ||
                    c.Text.Contains(input, StringComparison.OrdinalIgnoreCase));

                return match?.CityId;
            }

            var dto = new FlightSearchRequestDto
            {
                TripType = request.TripType,
                CabinClass = request.CabinClass
            };

            Guid? finalOriginId = null;
            Guid? finalDestId = null;

            if (request.TripType == TripType.MultiWay)
            {
                dto.Legs = request.MultiCityLegs
                    .Select(l => new { Leg = l, OriginId = ResolveId(l.OriginCity), DestId = ResolveId(l.DestinationCity) })
                    .Where(x => x.OriginId.HasValue && x.DestId.HasValue)
                    .Select(x => new FlightLegDto
                    {
                        OriginCityId = x.OriginId!.Value,
                        DestinationCityId = x.DestId!.Value,
                        DepartureDate = x.Leg.DepartureDate
                    }).ToList();

                if (dto.Legs.Any())
                {
                    finalOriginId = dto.Legs.First().OriginCityId;
                    finalDestId = dto.Legs.First().DestinationCityId;
                }
            }
            else
            {
                finalOriginId = ResolveId(request.OriginCity);
                finalDestId = ResolveId(request.DestinationCity);

                if (finalOriginId.HasValue && finalDestId.HasValue)
                {
                    dto.Legs.Add(new FlightLegDto
                    {
                        OriginCityId = finalOriginId.Value,
                        DestinationCityId = finalDestId.Value,
                        DepartureDate = request.DepartureDate
                    });

                    if (request.TripType == TripType.RoundTrip && request.ReturnDate.HasValue)
                    {
                        dto.ReturnDate = request.ReturnDate;
                    }
                }
            }

            if (dto.Legs.Count == 0 || !finalOriginId.HasValue || !finalDestId.HasValue)
            {
                return new SearchFlightsResult
                {
                    Success = false,
                    ErrorMessage = "We couldn't recognize one of the cities. Please select from the suggestions."
                };
            }

            // Newly wired in this phase (Phase 0 Finding #3): FlightSearchRequestValidator existed,
            // fully tested, but was never invoked anywhere before now. Called directly here rather
            // than relying on the Phase 2a ValidationBehavior pipeline picking it up automatically:
            // the pipeline validates the MediatR request (SearchFlightsQuery — raw free-text input)
            // before this handler runs, but what FlightSearchRequestValidator actually validates
            // (FlightSearchRequestDto — resolved city Guids) doesn't exist until the resolution step
            // above has already run. See Phase 2c report Flag F1.
            var validationResult = await _validator.ValidateAsync(dto, cancellationToken);
            if (!validationResult.IsValid)
            {
                return new SearchFlightsResult
                {
                    Success = false,
                    ErrorMessage = string.Join(" ", validationResult.Errors.Select(e => e.ErrorMessage))
                };
            }

            return new SearchFlightsResult
            {
                Success = true,
                OriginCityId = finalOriginId.Value,
                DestinationCityId = finalDestId.Value,
                DepartureDate = request.DepartureDate,
                TripType = request.TripType,
                ReturnDate = dto.ReturnDate
            };
        }
    }
}
