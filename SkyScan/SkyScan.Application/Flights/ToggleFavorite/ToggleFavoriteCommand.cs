using MediatR;

namespace SkyScan.Application.Flights.ToggleFavorite
{
    /// <summary>
    /// Relocated from FlightController.ToggleFavorite (Phase 2c). UserId is resolved by the
    /// controller (ASP.NET Identity concept) and passed in as a plain Guid.
    /// </summary>
    public class ToggleFavoriteCommand : IRequest<ToggleFavoriteResult>
    {
        public Guid UserId { get; set; }
        public string FlightNumber { get; set; } = string.Empty;
        public string DepartureTime { get; set; } = string.Empty;
        public string ArrivalTime { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string AirlineName { get; set; } = string.Empty;
        public string OriginIata { get; set; } = string.Empty;
        public string DestinationIata { get; set; } = string.Empty;
        public string RedirectUrl { get; set; } = string.Empty;
    }

    public class ToggleFavoriteResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public bool Favorited { get; set; }
    }
}
