using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkyScan.Core.Entities
{
    internal class City
    {
        public Guid CityId { get; set; }

        public string Name { get; set; }
        public string Country { get; set; }

        public List<Airport> Airports { get; set; }
    }
}
