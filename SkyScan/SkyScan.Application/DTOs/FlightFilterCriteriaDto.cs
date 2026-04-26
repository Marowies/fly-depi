using System;
using System.Collections.Generic;

namespace SkyScan.Application.DTOs
{
    public class FlightFilterCriteriaDto
    {
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        
        // 0 = Direct, 1 = 1 Stop, 2 = 2+ Stops
        public List<int> Stops { get; set; } = new();
        
        public List<string> Airlines { get; set; } = new();
        
        public List<TimeWindow> DepartureWindows { get; set; } = new();
        
        public FlightSortOption SortBy { get; set; } = FlightSortOption.Cheapest;
    }

    public enum TimeWindow
    {
        Morning,    // 06:00 - 12:00
        Afternoon,  // 12:00 - 18:00
        Evening,    // 18:00 - 00:00
        Night       // 00:00 - 06:00
    }

    public enum FlightSortOption
    {
        Cheapest,
        Fastest,
        Earliest
    }
}
