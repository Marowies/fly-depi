using SkyScan.Core.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkyScan.Core.Entities.AirLine
{
    public class Ticket
    {
        public Guid TicketId { get; set; }
        public decimal Price { get; set; }
        public CabinType CabinClass { get; set; }
        public Tuple<string, double> Luggage{ get; set; }
        public bool HasFood { get; set; } = false;
        public bool HasWifi { get; set; } = false;
        public bool HasEntertainment { get; set; } = false;

        public Guid FlightId { get; set; }
        public Flight Flight { get; set; }
    }
}
