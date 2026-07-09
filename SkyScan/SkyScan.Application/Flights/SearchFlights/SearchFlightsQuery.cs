using MediatR;
using SkyScan.Core.Constants;

namespace SkyScan.Application.Flights.SearchFlights
{
    /// <summary>Relocated from FlightController.Search() (Phase 2c) — resolves free-text city names to Guids, validates the assembled request, and returns redirect-ready parameters or an error.</summary>
    public class SearchFlightsQuery : IRequest<SearchFlightsResult>
    {
        public TripType TripType { get; set; } = TripType.OneWay;
        public string? OriginCity { get; set; }
        public string? DestinationCity { get; set; }
        public DateTime DepartureDate { get; set; }
        public DateTime? ReturnDate { get; set; }
        public string CabinClass { get; set; } = "economy";
        public List<SearchFlightsLegInput> MultiCityLegs { get; set; } = new();
    }

    public class SearchFlightsLegInput
    {
        public string? OriginCity { get; set; }
        public string? DestinationCity { get; set; }
        public DateTime DepartureDate { get; set; }
    }

    public class SearchFlightsResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Guid OriginCityId { get; set; }
        public Guid DestinationCityId { get; set; }
        public DateTime DepartureDate { get; set; }
        public TripType TripType { get; set; }
        public DateTime? ReturnDate { get; set; }
    }
}
