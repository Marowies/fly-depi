using Microsoft.AspNetCore.Identity;
using SkyScan.Core.Entities;
using SkyScan.Core.Repositories_Interfaces;
using System.Threading.Tasks;

namespace SkyScan.Infrastructure.Data.Repositories_Implementations
{
    public class UserRepository : IUserRepository
    {
        private readonly UserManager<User>   _userManager;
        private readonly SignInManager<User> _signInManager;

        public UserRepository(UserManager<User> userManager, SignInManager<User> signInManager)
        {
            _userManager   = userManager;
            _signInManager = signInManager;
        }

        // ── Basic Auth ────────────────────────────────────────────────────────────

        public async Task<IdentityResult> RegisterUserAsync(User user, string password)
            => await _userManager.CreateAsync(user, password);

        public async Task<SignInResult> LoginUserAsync(string email, string password, bool rememberMe)
            => await _signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: false);

        public async Task LogoutUserAsync()
            => await _signInManager.SignOutAsync();

        public async Task<User?> GetUserByEmailAsync(string email)
            => await _userManager.FindByEmailAsync(email);

        // ── Email Confirmation ────────────────────────────────────────────────────

        public async Task<string> GenerateEmailConfirmationTokenAsync(User user)
            => await _userManager.GenerateEmailConfirmationTokenAsync(user);

        public async Task<IdentityResult> ConfirmEmailAsync(User user, string token)
            => await _userManager.ConfirmEmailAsync(user, token);

        // ── Password Reset ────────────────────────────────────────────────────────

        public async Task<string> GeneratePasswordResetTokenAsync(User user)
            => await _userManager.GeneratePasswordResetTokenAsync(user);

        public async Task<IdentityResult> ResetPasswordAsync(User user, string token, string newPassword)
            => await _userManager.ResetPasswordAsync(user, token, newPassword);

        // ── Two-Factor Authentication ─────────────────────────────────────────────

        public async Task<bool> GetTwoFactorEnabledAsync(User user)
            => await _userManager.GetTwoFactorEnabledAsync(user);

        public async Task<IdentityResult> SetTwoFactorEnabledAsync(User user, bool enabled)
            => await _userManager.SetTwoFactorEnabledAsync(user, enabled);

        public async Task<string?> GetAuthenticatorKeyAsync(User user)
            => await _userManager.GetAuthenticatorKeyAsync(user);

        public async Task<IdentityResult> ResetAuthenticatorKeyAsync(User user)
            => await _userManager.ResetAuthenticatorKeyAsync(user);

        public async Task<bool> VerifyTwoFactorTokenAsync(User user, string token)
            => await _userManager.VerifyTwoFactorTokenAsync(user, _userManager.Options.Tokens.AuthenticatorTokenProvider, token);

        public async Task<SignInResult> TwoFactorSignInAsync(string provider, string code, bool rememberMe, bool rememberMachine)
            => await _signInManager.TwoFactorSignInAsync(provider, code, rememberMe, rememberMachine);

        // ── Cookie Refresh ────────────────────────────────────────────────────────

        public async Task RefreshSignInAsync(User user)
            => await _signInManager.RefreshSignInAsync(user);
    }
}
