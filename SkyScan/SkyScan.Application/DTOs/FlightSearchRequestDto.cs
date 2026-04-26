using SkyScan.Core.Constants;
using System;
using System.Collections.Generic;

namespace SkyScan.Application.DTOs
{
    public class FlightSearchRequestDto
    {
        public TripType TripType { get; set; } = TripType.OneWay;
        public List<FlightLegDto> Legs { get; set; } = new();
        public int Adults { get; set; } = 1;
        public string CabinClass { get; set; } = "economy";
        
        // For RoundTrip convenience, we often use a single ReturnDate property 
        // that maps to the second leg's departure date.
        public DateTime? ReturnDate { get; set; }
    }
}
