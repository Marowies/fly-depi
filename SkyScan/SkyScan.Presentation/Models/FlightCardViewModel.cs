using SkyScan.Application.DTOs;

namespace SkyScan.Presentation.Models
{
    /// <summary>
    /// Wraps a single FlightDto with the display currency and route context (raw IATA codes)
    /// a flight card needs — shared by the outbound and return sections of the results page.
    /// </summary>
    public class FlightCardViewModel
    {
        public required FlightDto Flight { get; set; }
        public decimal ConvertedPrice { get; set; }
        public string CurrencySymbol { get; set; } = "$";
        public string RouteOriginIata { get; set; } = string.Empty;
        public string RouteDestinationIata { get; set; } = string.Empty;
        public string Label { get; set; } = "Outbound";
    }
}
