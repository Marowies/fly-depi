using Microsoft.AspNetCore.Mvc.Rendering;
using SkyScan.Core.Constants;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SkyScan.Presentation.Models
{
    public class FlightSearchViewModel
    {
        public TripType TripType { get; set; } = TripType.OneWay;

        // For OneWay and RoundTrip, we use the first leg
        public string? OriginCity { get; set; }
        public string? DestinationCity { get; set; }
        
        [DataType(DataType.Date)]
        public DateTime DepartureDate { get; set; } = DateTime.Today.AddDays(1);

        [DataType(DataType.Date)]
        public DateTime? ReturnDate { get; set; }

        // For Multi-city
        public List<MultiCityLegViewModel> MultiCityLegs { get; set; } = new();

        public List<SelectListItem>? CitiesWithAirports { get; set; }
        
        public int Adults { get; set; } = 1;
        public string CabinClass { get; set; } = "economy";
    }

    public class MultiCityLegViewModel
    {
        public string? OriginCity { get; set; }
        public string? DestinationCity { get; set; }
        public DateTime DepartureDate { get; set; } = DateTime.Today.AddDays(1);
    }
}
