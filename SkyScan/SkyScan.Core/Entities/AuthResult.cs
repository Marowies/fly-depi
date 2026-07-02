using System.Collections.Generic;
using System.Linq;

namespace SkyScan.Core.Entities
{
    /// <summary>
    /// Framework-agnostic outcome of an authentication/account operation — replaces
    /// Microsoft.AspNetCore.Identity's IdentityResult/SignInResult in the Core contract
    /// (IUserRepository) so Core never references the Identity package.
    /// </summary>
    public class AuthResult
    {
        public bool Succeeded { get; init; }
        public bool IsLockedOut { get; init; }
        public bool RequiresTwoFactor { get; init; }
        public IEnumerable<string> Errors { get; init; } = Enumerable.Empty<string>();

        public static AuthResult Success() => new() { Succeeded = true };
        public static AuthResult Failed(params string[] errors) => new() { Succeeded = false, Errors = errors };
        public static AuthResult LockedOut() => new() { Succeeded = false, IsLockedOut = true };
        public static AuthResult TwoFactorRequired() => new() { Succeeded = false, RequiresTwoFactor = true };
    }
}
