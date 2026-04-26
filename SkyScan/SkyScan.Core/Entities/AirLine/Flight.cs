using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SkyScan.Core.Entities.AirLine
{
    public class Flight
    {
        [Key]
        public Guid FlightId { get; set; }

        [Required]
        public Guid AirlineId { get; set; }
        public Airline Airline { get; set; }

        [Required]
        public Guid AirplaneId { get; set; }
        public Airplane Airplane { get; set; }

        [Required]
        [StringLength(20)]
        public string FlightNumber { get; set; }

        [Required]
        public Guid DepartureAirportId { get; set; }

        [Required]
        public Guid ArrivalAirportId { get; set; }

        public Airport DepartureAirport { get; set; }
        public Airport ArrivalAirport { get; set; }

        [Required]
        public DateTime DepartureTime { get; set; }

        [Required]
        public DateTime ArrivalTime { get; set; }

        public TimeSpan Duration { get { return ArrivalTime - DepartureTime; } }

        [Url]
        public string RedirectURL { get; set; }

        public List<Ticket> Tickets { get; set; } = new List<Ticket>();
    }
}
