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

        [Required]
        [Phone]
        [StringLength(20)]
        public string HotlineNumber { get; set; }

        [StringLength(3, MinimumLength = 2, ErrorMessage = "IATA Code must be 2-3 characters.")]
        public string? IataCode { get; set; }

        [StringLength(3, MinimumLength = 3, ErrorMessage = "ICAO Code must be 3 characters.")]
        public string? IcaoCode { get; set; }

        [StringLength(50)]
        public string? Callsign { get; set; }

        public List<Airplane> Airplanes { get; set; } = new List<Airplane>();
        public List<Flight> Flights { get; set; } = new List<Flight>();
    }
}
