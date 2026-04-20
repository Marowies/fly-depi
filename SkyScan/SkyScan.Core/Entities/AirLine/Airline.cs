using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkyScan.Core.Entities.AirLine
{
    public class Airline
    {
        public Guid AirlineId { get; set; }
        public string Name { get; set; }
        public string HotlineNumber { get; set; }
        public List<Airplane> Airplanes { get; set; } = new List<Airplane>();
        public List<Flight> Flights { get; set; } = new List<Flight>();

    }
}
