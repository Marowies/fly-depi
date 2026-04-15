using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkyScan.Core.Entities
{
    internal class Perks
    {
        public Guid PerkId { get; set; }

        public bool HasFood { get; set; }
        public bool HasWifi { get; set; }
        public bool HasEntertainment { get; set; }

        public Guid CabinClassId { get; set; }
        public CabinClass CabinClass { get; set; }
    }
}
