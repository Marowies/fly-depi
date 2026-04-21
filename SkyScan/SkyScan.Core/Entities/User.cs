using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SkyScan.Core.Entities
{
    public class User
    {
        [Key]
        public Guid UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; }

        public List<Search> Searches { get; set; } = new List<Search>();
        public List<PriceAlert> PriceAlerts { get; set; } = new List<PriceAlert>();
    }
}
