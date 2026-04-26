using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SkyScan.Core.Entities
{
    public class Airport
    {
        [Key]
        public Guid AirportId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [StringLength(10)]
        public string Code { get; set; }

        [StringLength(3, MinimumLength = 3, ErrorMessage = "IATA Code must be 3 characters.")]
        public string? IataCode { get; set; }

        [StringLength(4, MinimumLength = 4, ErrorMessage = "ICAO Code must be 4 characters.")]
        public string? IcaoCode { get; set; }

        [StringLength(50)]
        public string? Type { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int? ElevationFt { get; set; }

        [Required]
        public Guid CityId { get; set; }
        public City City { get; set; }
    }
}
