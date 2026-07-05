using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SkyScan.Core.Entities.AirLine
{
    public class Airline
    {
        [Key]
        public Guid AirlineId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }


        [StringLength(3, MinimumLength = 2, ErrorMessage = "IATA Code must be 2-3 characters.")]
        public string? IataCode { get; set; }

        [StringLength(255)]
        public string? Url { get; set; }

        public List<Flight> Flights { get; set; } = new List<Flight>();
    }
}
