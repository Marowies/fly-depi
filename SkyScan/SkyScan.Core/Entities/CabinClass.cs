using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkyScan.Core.Entities
{
    internal class CabinClass
    {
        public Guid ClassId { get; set; }

        public string Type { get; set; }
        public string LegRoomSize { get; set; }

        public Perks Perks { get; set; }
    }
}
