namespace SkyScan.Application.DTOs
{
    public class FlightDto
    {
        public string AirlineName { get; set; } = string.Empty;
        public string FlightNumber { get; set; } = string.Empty;
        public string OriginAirport { get; set; } = string.Empty;
        public string DestinationAirport { get; set; } = string.Empty;
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public decimal Price { get; set; }
        public int Stops { get; set; } = 0; // 0 for direct
        public TimeSpan Duration => ArrivalTime - DepartureTime;
        public string Status { get; set; } = string.Empty;
        public string? RedirectURL { get; set; }
    }
}
