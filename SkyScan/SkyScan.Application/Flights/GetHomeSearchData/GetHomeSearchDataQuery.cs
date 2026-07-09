using MediatR;
using SkyScan.Application.Flights.Common;

namespace SkyScan.Application.Flights.GetHomeSearchData
{
    /// <summary>Relocated from FlightController.Index() (Phase 2c) — the airport dropdown plus up to 5 trending routes, with a DB-pairing fallback when fewer than 5 real trending routes exist.</summary>
    public class GetHomeSearchDataQuery : IRequest<HomeSearchDataResult>
    {
    }

    public class HomeSearchDataResult
    {
        public List<CityDropdownItem> CitiesDropdown { get; set; } = new();
        public List<TrendingRouteResult> TrendingRoutes { get; set; } = new();
    }

    public class TrendingRouteResult
    {
        public Guid OriginCityId { get; set; }
        public Guid DestinationCityId { get; set; }
        public string OriginCityName { get; set; } = string.Empty;
        public string DestinationCityName { get; set; } = string.Empty;
        public int SearchCount { get; set; }
    }
}
