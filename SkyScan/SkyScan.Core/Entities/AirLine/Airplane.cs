using System;
using System.ComponentModel.DataAnnotations;

namespace SkyScan.Core.Entities.AirLine
{
    public class Airplane
    {
        [Key]
        public Guid AirplaneId { get; set; }

        [Required]
        [StringLength(10)]
        public string AircraftCode { get; set; }

        
        [StringLength(150)]
        public string? AircraftName { get; set; }
    }
}
