using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SkyScan.Core.Entities
{
    public class City
    {
        [Key]
        public Guid CityId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(100)]
        public string? NameAr { get; set; }

        [Required]
        [StringLength(2)]
        public string CountryCode { get; set; }
        [StringLength(3)]
        public string? IataCode { get; set; }

        public Country Country { get; set; }


        public int SearchCount { get; set; } = 0;

        public List<Airport> Airports { get; set; } = new List<Airport>();
    }
}
