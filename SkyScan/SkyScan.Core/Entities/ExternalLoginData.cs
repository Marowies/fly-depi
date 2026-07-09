namespace SkyScan.Core.Entities
{
    /// <summary>
    /// Framework-agnostic projection of ASP.NET Identity's ExternalLoginInfo — keeps
    /// IUserRepository's contract free of any direct Microsoft.AspNetCore.Identity type,
    /// same rationale as AuthResult wrapping IdentityResult/SignInResult.
    /// </summary>
    public class ExternalLoginData
    {
        public bool Found { get; init; }
        public string LoginProvider { get; init; } = string.Empty;
        public string ProviderKey { get; init; } = string.Empty;
        public string? ProviderDisplayName { get; init; }
        public string? Email { get; init; }
        public string? Name { get; init; }

        public static ExternalLoginData NotFound() => new() { Found = false };
    }
}
