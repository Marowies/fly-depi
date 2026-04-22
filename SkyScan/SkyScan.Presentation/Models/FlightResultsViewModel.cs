using SkyScan.Application.DTOs;

namespace SkyScan.Presentation.Models
{
    public class FlightResultsViewModel
    {
        public string OriginIata { get; set; }
        public string DestinationIata { get; set; }
        public string OriginCity { get; set; }
        public string DestinationCity { get; set; }
        public DateTime DepartureDate { get; set; }
        public IEnumerable<FlightDto> Flights { get; set; } = new List<FlightDto>();
    }
}
