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
        public decimal TotalCost { get; set; }

        public Guid CabinClassId { get; set; }
        public CabinClass CabinClass { get; set; }

        public Guid FlightId { get; set; }
        public Flight Flight { get; set; }
    }
}
