using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SkyScan.Core.Entities
{
    public class Country
    {
        [Key]
        [StringLength(2, MinimumLength = 2, ErrorMessage = "CountryCode must be exactly 2 characters.")]
        public string CountryCode { get; set; } // Alpha-2 code (e.g., "US", "EG")

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(10)]
        public string Continent { get; set; }

        public List<City> Cities { get; set; } = new List<City>();
    }
}
