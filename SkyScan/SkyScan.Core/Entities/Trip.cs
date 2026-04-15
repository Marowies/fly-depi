using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkyScan.Core.Entities
{
    internal class Trip
    {
        public Guid TripId { get; set; }

        public string TripNumber { get; set; }

        public int Stops { get; set; }

        public List<Flight> Flights { get; set; }
    }
}
