using MediatR;

namespace SkyScan.Application.Flights.GetNearestCity
{
    /// <summary>Relocated from FlightController.GetNearestCity (Phase 2c) — GPS coordinates to a nearby, localized city name for the "Detect my location" button.</summary>
    public class GetNearestCityQuery : IRequest<NearestCityResult>
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
    }

    public class NearestCityResult
    {
        public bool Found { get; set; }
        public Guid CityId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }
}
