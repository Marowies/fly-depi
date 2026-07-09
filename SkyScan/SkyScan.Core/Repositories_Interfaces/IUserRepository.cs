using SkyScan.Core.Entities;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SkyScan.Core.Repositories_Interfaces
{
    public interface IUserRepository
    {
        // Basic Auth
        Task<AuthResult> RegisterUserAsync(User user, string password);
        Task<AuthResult> LoginUserAsync(string email, string password, bool rememberMe);
        Task LogoutUserAsync();
        Task<User?> GetUserByEmailAsync(string email);
        Task<User?> GetUserByIdAsync(Guid id);

        /// <summary>Resolves the signed-in Identity user for the current request's ClaimsPrincipal.
        /// ClaimsPrincipal is a plain BCL type (System.Security.Claims), not ASP.NET-Core-specific,
        /// so this stays framework-agnostic the same way AuthResult does.</summary>
        Task<User?> GetCurrentUserAsync(ClaimsPrincipal principal);

        // Email Confirmation
        Task<string> GenerateEmailConfirmationTokenAsync(User user);
        Task<AuthResult> ConfirmEmailAsync(User user, string token);

        // Password Reset (Forgot Password)
        Task<string> GeneratePasswordResetTokenAsync(User user);
        Task<AuthResult> ResetPasswordAsync(User user, string token, string newPassword);
        Task<AuthResult> ChangePasswordAsync(User user, string oldPassword, string newPassword);

        // Two-Factor Authentication
        Task<bool> GetTwoFactorEnabledAsync(User user);
        Task<AuthResult> SetTwoFactorEnabledAsync(User user, bool enabled);
        Task<string?> GetAuthenticatorKeyAsync(User user);
        Task<AuthResult> ResetAuthenticatorKeyAsync(User user);
        Task<bool> VerifyTwoFactorTokenAsync(User user, string token);
        Task<AuthResult> TwoFactorSignInAsync(string provider, string code, bool rememberMe, bool rememberMachine);
        Task<User?> GetTwoFactorAuthenticationUserAsync();

        // External (Google) Login
        Task<ExternalLoginData> GetExternalLoginInfoAsync();
        Task<AuthResult> ExternalLoginSignInAsync(string loginProvider, string providerKey);
        Task<AuthResult> LinkExternalLoginAsync(User user, string loginProvider, string providerKey, string? providerDisplayName);

        // Cookie Refresh
        Task RefreshSignInAsync(User user);
    }
}
