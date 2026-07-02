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

        // Round trip: populated only when IsRoundTrip is true
        public bool IsRoundTrip { get; set; }
        public DateTime? ReturnDate { get; set; }
        public IEnumerable<FlightDto> ReturnFlights { get; set; } = new List<FlightDto>();

        // Metadata for filtering (outbound + return combined so the shared price slider covers both)
        public decimal MinPrice => AllFlights.Any() ? AllFlights.Min(f => f.Price) : 0;
        public decimal MaxPrice => AllFlights.Any() ? AllFlights.Max(f => f.Price) : 0;
        public IEnumerable<string> UniqueAirlines => AllFlights.Select(f => f.AirlineName).Distinct().OrderBy(a => a);

        private IEnumerable<FlightDto> AllFlights
        {
            get
            {
                var list = new List<FlightDto>();
                foreach (var f in Flights)
                {
                    list.Add(f);
                    if (f.ReturnLeg != null)
                    {
                        list.Add(f.ReturnLeg);
                    }
                }
                return list;
            }
        }
    }
}
