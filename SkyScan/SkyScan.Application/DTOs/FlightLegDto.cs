using System;

namespace SkyScan.Application.DTOs
{
    public class FlightLegDto
    {
        public Guid OriginCityId { get; set; }
        public string OriginCityName { get; set; } = string.Empty;
        public Guid DestinationCityId { get; set; }
        public string DestinationCityName { get; set; } = string.Empty;
        public DateTime DepartureDate { get; set; }
    }
}
