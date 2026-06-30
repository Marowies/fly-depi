using SkyScan.Core.Constants;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SkyScan.Core.Entities.AirLine
{
    public class Ticket
    {
        [Key]
        public Guid TicketId { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 1000000, ErrorMessage = "Price must be greater than zero.")]
        public decimal Price { get; set; }

        
        [StringLength(3)]
        public string? Currency { get; set; }

        [Required]
        public CabinType CabinClass { get; set; }

        public bool HasFood { get; set; } = false;
        public bool HasWifi { get; set; } = false;
        public bool HasEntertainment { get; set; } = false;

        [Required]
        public Guid FlightId { get; set; }
        public Flight Flight { get; set; }
    }
}
