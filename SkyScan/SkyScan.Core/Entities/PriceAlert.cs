using System;
using System.ComponentModel.DataAnnotations;

namespace SkyScan.Core.Entities
{
    public class PriceAlert
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [Required]
        public Guid TripId { get; set; }
        public Trip Trip { get; set; }

        // Column type (decimal(18,2)) is configured in PriceAlertConfiguration, not here —
        // storage metadata belongs in the Fluent API configuration, not on the domain entity.
        [Required]
        [Range(0, 1000000)]
        public decimal TargetPrice { get; set; }
    }
}
