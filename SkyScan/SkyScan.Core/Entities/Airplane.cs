using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkyScan.Core.Entities
{
    internal class Airplane
    {
        public Guid AirplaneId { get; set; }

        public string Model { get; set; }

        public List<CabinClass> CabinClasses { get; set; }
    }
}
