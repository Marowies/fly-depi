using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkyScan.Core.Entities
{
    internal class Airline
    {
        public Guid AirlineId { get; set; }
        public string Name { get; set; }
        public string HotlineNumber { get; set; }

        public List<Flight> Flights { get; set; }

    }
}
