using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SkyScan.Core.Entities
{
    public class Country
    {
        [Key]
        public Guid CountryId { get; set; }

        [Required]
        [StringLength(2)]
        public string Code { get; set; } // Alpha-2 code

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(10)]
        public string Continent { get; set; }

        public List<City> Cities { get; set; } = new List<City>();
    }
}
