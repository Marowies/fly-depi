using Microsoft.AspNetCore.Identity;
using SkyScan.Core.Entities;
using System.Linq;

namespace SkyScan.Infrastructure.Identity
{
    /// <summary>
    /// Maps ASP.NET Identity's result types to the framework-agnostic AuthResult that
    /// IUserRepository exposes to Core — the only place Identity's result shapes should
    /// ever be touched.
    /// </summary>
    public static class IdentityResultExtensions
    {
        public static AuthResult ToAuthResult(this IdentityResult result) =>
            result.Succeeded
                ? AuthResult.Success()
                : AuthResult.Failed(result.Errors.Select(e => e.Description).ToArray());

        public static AuthResult ToAuthResult(this SignInResult result)
        {
            if (result.Succeeded) return AuthResult.Success();
            if (result.IsLockedOut) return AuthResult.LockedOut();
            if (result.RequiresTwoFactor) return AuthResult.TwoFactorRequired();
            return AuthResult.Failed("Invalid login attempt.");
        }

        public static User ToDomain(this ApplicationUser appUser) => new()
        {
            Id = appUser.Id,
            Name = appUser.Name,
            Email = appUser.Email ?? string.Empty,
            EmailConfirmed = appUser.EmailConfirmed
        };
    }
}
