using System;

namespace SkyScan.Presentation.Models
{
    public class BookFlightRequest
    {
        public string FlightNumber { get; set; } = string.Empty;
        public string DepartureTime { get; set; } = string.Empty;
        public string Origin { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string OriginIata { get; set; } = string.Empty;
        public string DestinationIata { get; set; } = string.Empty;
        public string AirlineName { get; set; } = string.Empty;
        public string ArrivalTime { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string RedirectUrl { get; set; } = string.Empty;
        public bool HasWifi { get; set; }
        public bool HasFood { get; set; }
        public bool HasEntertainment { get; set; }

        // Return Leg Details (Optional)
        public string? ReturnFlightNumber { get; set; }
        public string? ReturnDepartureTime { get; set; }
        public string? ReturnOrigin { get; set; }
        public string? ReturnDestination { get; set; }
        public string? ReturnOriginIata { get; set; }
        public string? ReturnDestinationIata { get; set; }
        public string? ReturnAirlineName { get; set; }
        public string? ReturnArrivalTime { get; set; }
        public bool ReturnHasWifi { get; set; }
        public bool ReturnHasFood { get; set; }
        public bool ReturnHasEntertainment { get; set; }

        // Flow Customizations
        public bool CreatePriceAlert { get; set; }
        public bool AddToCalendar { get; set; }
    }
}
