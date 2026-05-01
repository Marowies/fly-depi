using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SkyScan.Core.Entities
{
    public class User : IdentityUser<Guid>
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        public List<Search> Searches { get; set; } = new List<Search>();
        public List<PriceAlert> PriceAlerts { get; set; } = new List<PriceAlert>();
    }
}

