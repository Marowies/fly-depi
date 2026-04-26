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
        
        // Metadata for filtering
        public decimal MinPrice => Flights.Any() ? Flights.Min(f => f.Price) : 0;
        public decimal MaxPrice => Flights.Any() ? Flights.Max(f => f.Price) : 0;
        public IEnumerable<string> UniqueAirlines => Flights.Select(f => f.AirlineName).Distinct().OrderBy(a => a);
    }
}
