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
        public Guid OriginCityId { get; set; }
        public Guid DestinationCityId { get; set; }
        public City OriginCity { get; set; }
        public City DestinationCity { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; }
    }
}
