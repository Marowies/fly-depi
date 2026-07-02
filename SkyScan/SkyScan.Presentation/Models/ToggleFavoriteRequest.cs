namespace SkyScan.Presentation.Models
{
    /// <summary>
    /// Everything needed to find-or-create the Flight a favorite/alert points to. No provider
    /// persists Flight rows on search anymore, so this data — already rendered on every result
    /// card — lets ToggleFavorite materialize one on demand instead of requiring it pre-exist.
    /// </summary>
    public class ToggleFavoriteRequest
    {
        public string FlightNumber { get; set; } = string.Empty;
        public string DepartureTime { get; set; } = string.Empty;
        public string ArrivalTime { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string AirlineName { get; set; } = string.Empty;
        public string OriginIata { get; set; } = string.Empty;
        public string DestinationIata { get; set; } = string.Empty;
        public string RedirectUrl { get; set; } = string.Empty;
    }
}
