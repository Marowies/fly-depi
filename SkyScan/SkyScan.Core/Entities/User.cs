using System;
using System.ComponentModel.DataAnnotations;

namespace SkyScan.Core.Entities
{
    /// <summary>
    /// Plain domain profile — deliberately has no dependency on ASP.NET Identity. The
    /// Identity-backed persistence type (<c>ApplicationUser</c>) lives in Infrastructure and
    /// maps to/from this shape at the repository boundary.
    /// </summary>
    public class User
    {
        public Guid Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        public bool EmailConfirmed { get; set; }
    }
}
