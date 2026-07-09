using MediatR;
using SkyScan.Application.Flights.Common;

namespace SkyScan.Application.Flights.GetCityDropdown
{
    public class GetCityDropdownQueryHandler : IRequestHandler<GetCityDropdownQuery, List<CityDropdownItem>>
    {
        private readonly AirportDropdownCache _dropdownCache;

        public GetCityDropdownQueryHandler(AirportDropdownCache dropdownCache)
        {
            _dropdownCache = dropdownCache;
        }

        public async Task<List<CityDropdownItem>> Handle(GetCityDropdownQuery request, CancellationToken cancellationToken)
        {
            return await _dropdownCache.GetAsync();
        }
    }
}
