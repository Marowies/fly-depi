using Microsoft.AspNetCore.Identity;
using SkyScan.Core.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SkyScan.Infrastructure.Identity
{
    /// <summary>
    /// The actual EF/Identity-backed user row (table AspNetUsers). Only Infrastructure and
    /// Presentation (via UserManager/SignInManager, which are framework types, not ours) ever
    /// see this type — Core stays on the plain <see cref="User"/> domain shape.
    /// </summary>
    public class ApplicationUser : IdentityUser<Guid>
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public List<Search> Searches { get; set; } = new List<Search>();
        public List<PriceAlert> PriceAlerts { get; set; } = new List<PriceAlert>();

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAtUtc { get; set; }
    }
}
