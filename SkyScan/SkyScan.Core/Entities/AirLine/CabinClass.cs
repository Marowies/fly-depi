using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkyScan.Core.Entities.AirLine
{
    public class CabinClass
    {
        public Guid Id { get; set; }

        public CabinType Type { get; set; }
        public int LegRoomSize { get; set; }

        public bool HasFood { get; set; }
        public bool HasWifi { get; set; }
        public bool HasEntertainment { get; set; }
    }
}
