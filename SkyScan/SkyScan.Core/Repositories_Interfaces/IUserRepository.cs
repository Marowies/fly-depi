using SkyScan.Core.Entities;
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

        // Email Confirmation
        Task<string> GenerateEmailConfirmationTokenAsync(User user);
        Task<AuthResult> ConfirmEmailAsync(User user, string token);

        // Password Reset (Forgot Password)
        Task<string> GeneratePasswordResetTokenAsync(User user);
        Task<AuthResult> ResetPasswordAsync(User user, string token, string newPassword);

        // Two-Factor Authentication
        Task<bool> GetTwoFactorEnabledAsync(User user);
        Task<AuthResult> SetTwoFactorEnabledAsync(User user, bool enabled);
        Task<string?> GetAuthenticatorKeyAsync(User user);
        Task<AuthResult> ResetAuthenticatorKeyAsync(User user);
        Task<bool> VerifyTwoFactorTokenAsync(User user, string token);
        Task<AuthResult> TwoFactorSignInAsync(string provider, string code, bool rememberMe, bool rememberMachine);

        // Cookie Refresh
        Task RefreshSignInAsync(User user);
    }
}
