using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkyScan.Core.Entities.AirLine
{
    public class Flight
    {
        public Guid FlightId { get; set; }

        public Guid AirlineId { get; set; }
        public Airline Airline { get; set; }
        public Guid AirplaneId { get; set; }
        public Airplane Airplane { get; set; }
        public string FlightNumber { get; set; }

        public Guid DepartureAirportId { get; set; }
        public Guid ArrivalAirportId { get; set; }
        public Airport DepartureAirport { get; set; }
        public Airport ArrivalAirport { get; set; }
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public TimeSpan Duration { get { return ArrivalTime - DepartureTime; } }
        public string RedirectURL { get; set; }
        public List<Ticket> Tickets { get; set; }
    }
}
