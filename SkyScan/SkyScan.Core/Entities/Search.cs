using SkyScan.Core.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkyScan.Core.Entities
{
    public class Search
    {
        public Guid SearchId { get; set; }
        public DateTime TimeStamp { get; set; }
        public TripType Type { get; set; }
        public DateTime DepartureDate { get; set; }
        public Guid OriginAirportId { get; set; }
        public Guid DestinationAirportId { get; set; }
        public Airport OriginAirport { get; set; }
        public Airport DestinationAirport { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; }

    }
}
