using MediatR;
using SkyScan.Application.DTOs;
using SkyScan.Core.Constants;

namespace SkyScan.Application.Flights.GetFlightResults
{
    /// <summary>
    /// Relocated from FlightController.Results() (Phase 2c). Date/Guid parsing and the
    /// past-date redirect stay in the controller (routing concerns); this Query covers
    /// everything from search-logging onward. CurrentUserId is resolved by the controller
    /// from ClaimsPrincipal/UserManager (an ASP.NET Identity concept the Application layer
    /// doesn't depend on) and passed in as a plain Guid?.
    /// </summary>
    public class GetFlightResultsQuery : IRequest<FlightResultsResult>
    {
        public Guid OriginCityId { get; set; }
        public Guid DestinationCityId { get; set; }
        public DateTime DepartureDate { get; set; }
        public TripType TripType { get; set; }
        public DateTime? ReturnDate { get; set; }
        public Guid? CurrentUserId { get; set; }
    }

    public class FlightResultsResult
    {
        public string OriginIata { get; set; } = string.Empty;
        public string DestinationIata { get; set; } = string.Empty;
        public string OriginCity { get; set; } = string.Empty;
        public string DestinationCity { get; set; } = string.Empty;
        public DateTime DepartureDate { get; set; }
        public bool IsRoundTrip { get; set; }
        public DateTime? ReturnDate { get; set; }
        public List<FlightDto> Flights { get; set; } = new();
    }
}
