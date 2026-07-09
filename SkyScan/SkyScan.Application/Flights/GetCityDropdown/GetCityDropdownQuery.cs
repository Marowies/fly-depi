using MediatR;
using SkyScan.Application.Flights.Common;

namespace SkyScan.Application.Flights.GetCityDropdown
{
    /// <summary>Thin wrapper over AirportDropdownCache, used wherever only the dropdown (not trending routes) is needed — e.g. re-rendering the search form after a validation failure.</summary>
    public class GetCityDropdownQuery : IRequest<List<CityDropdownItem>>
    {
    }
}
