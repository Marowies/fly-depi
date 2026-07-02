using System;
using System.ComponentModel.DataAnnotations;
using SkyScan.Core.Entities.AirLine;

namespace SkyScan.Core.Entities
{
    public class Booking
    {
        [Key]
        public Guid BookingId { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [Required]
        public Guid FlightId { get; set; }
        public Flight Flight { get; set; }

        public DateTime BookingDate { get; set; } = DateTime.UtcNow;
    }
}
