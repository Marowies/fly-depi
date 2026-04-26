using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SkyScan.Core.Constants;

namespace SkyScan.Core.Entities.AirLine
{
    public class Airplane
    {
        [Key]
        public Guid AirplaneId { get; set; }

        [Required]
        [StringLength(100)]
        public string Model { get; set; }

        
        [StringLength(100)] 
        public string ? ManufactureCompany { get; set; } 

        [Required]
        [StringLength(100)]
        public string OwnerCompany { get; set; }

        [Required]
        public DateOnly ManufactureDate { get; set; }

    
        public DateOnly ? StartDate { get; set; }

        
        public DateOnly ? EndDate { get; set; }

        [StringLength(20)]
        public string? Icao24 { get; set; }

        [StringLength(20)]
        public string? Registration { get; set; }

        [Required]
        [StringLength(50)]
        public string PlaneId { get; set; }

        [StringLength(100)]
        public string? SerialNumber { get; set; }

        [StringLength(50)]
        public string? EngineType { get; set; }

        [StringLength(20)]
        public string? Status { get; set; }

        [Range(1, 1000)]
        public int Seats { get; set; }

        public List<CabinType> CabinClasses { get; set; } = new List<CabinType>();
    }
}
