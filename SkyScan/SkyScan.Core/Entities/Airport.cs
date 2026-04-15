using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkyScan.Core.Entities
{
    public class Airport
    {
        public Guid AirportId { get; set; }

        public string Name { get; set; }
        public string Code { get; set; }

        public Guid CityId { get; set; }
        public City City { get; set; }
    }
}
