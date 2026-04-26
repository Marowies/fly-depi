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

        [Required]
        [StringLength(2)]
        public string CountryCode { get; set; }
        public Country Country { get; set; }

        public List<Airport> Airports { get; set; } = new List<Airport>();
    }
}
