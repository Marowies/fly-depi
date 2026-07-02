using SkyScan.Core.Constants;
using System;
using System.ComponentModel.DataAnnotations;

namespace SkyScan.Core.Entities
{
    public class Search
    {
        
        public DateTime TimeStamp { get; set; }
        public TripType Type { get; set; }
        public DateTime DepartureDate { get; set; }

        public Guid OriginCityId { get; set; }
        public Guid DestinationCityId { get; set; }
        public City OriginCity { get; set; }
        public City DestinationCity { get; set; }

        public Guid UserId { get; set; }
    }
}
