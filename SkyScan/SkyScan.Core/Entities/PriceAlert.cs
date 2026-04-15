using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkyScan.Core.Entities
{
    internal class PriceAlert
    {
        public Guid AlertId { get; set; }

        public Guid UserId { get; set; }
        public User User { get; set; }

        public Guid TripId { get; set; }
        public Trip Trip { get; set; }

        public decimal TargetPrice { get; set; }
    }
}
