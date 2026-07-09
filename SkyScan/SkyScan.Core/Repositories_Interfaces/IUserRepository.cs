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

        // Two-Fa