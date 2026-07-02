using Microsoft.AspNetCore.Identity;
using SkyScan.Core.Entities;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Infrastructure.Identity;
using System;
using System.Threading.Tasks;

namespace SkyScan.Infrastructure.Data.Repositories_Implementations
{
    public class UserRepository : IUserRepository
    {
        private readonly UserManager<ApplicationUser>   _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public UserRepository(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _userManager   = userManager;
            _signInManager = signInManager;
        }

        // ── Basic Auth ────────────────────────────────────────────────────────────

        public async Task<AuthResult> RegisterUserAsync(User user, string password)
        {
            var appUser = new ApplicationUser
            {
                Id             = user.Id == Guid.Empty ? Guid.NewGuid() : user.Id,
                UserName       = user.Email,
                Email          = user.Email,
                Name           = user.Name,
                EmailConfirmed = user.EmailConfirmed
            };

            var result = await _userManager.CreateAsync(appUser, password);
            if (result.Succeeded)
            {
                user.Id = appUser.Id; // propagate the generated id back to the caller's domain object
            }
            return result.ToAuthResult();
        }

        public async Task<AuthResult> LoginUserAsync(string email, string password, bool rememberMe)
            => (await _signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: true)).ToAuthResult();

        public async Task LogoutUserAsync()
            => await _signInManager.SignOutAsync();

        public async Task<User?> GetUserByEmailAsync(string email)
            => (await _userManager.FindByEmailAsync(email))?.ToDomain();

        // ── Email Confirmation ────────────────────────────────────────────────────

        public async Task<string> GenerateEmailConfirmationTokenAsync(User user)
            => await _userManager.GenerateEmailConfirmationTokenAsync(await RequireAppUserAsync(user));

        public async Task<AuthResult> ConfirmEmailAsync(User user, string token)
            => (await _userManager.ConfirmEmailAsync(await RequireAppUserAsync(user), token)).ToAuthResult();

        // ── Password Reset ────────────────────────────────────────────────────────

        public async Task<string> GeneratePasswordResetTokenAsync(User user)
            => await _userManager.GeneratePasswordResetTokenAsync(await RequireAppUserAsync(user));

        public async Task<AuthResult> ResetPasswordAsync(User user, string token, string newPassword)
            => (await _userManager.ResetPasswordAsync(await RequireAppUserAsync(user), token, newPassword)).ToAuthResult();

        // ── Two-Factor Authentication ─────────────────────────────────────────────

        public async Task<bool> GetTwoFactorEnabledAsync(User user)
            => await _userManager.GetTwoFactorEnabledAsync(await RequireAppUserAsync(user));

        public async Task<AuthResult> SetTwoFactorEnabledAsync(User user, bool enabled)
            => (await _userManager.SetTwoFactorEnabledAsync(await RequireAppUserAsync(user), enabled)).ToAuthResult();

        public async Task<string?> GetAuthenticatorKeyAsync(User user)
            => await _userManager.GetAuthenticatorKeyAsync(await RequireAppUserAsync(user));

        public async Task<AuthResult> ResetAuthenticatorKeyAsync(User user)
            => (await _userManager.ResetAuthenticatorKeyAsync(await RequireAppUserAsync(user))).ToAuthResult();

        public async Task<bool> VerifyTwoFactorTokenAsync(User user, string token)
        {
            var appUser = await RequireAppUserAsync(user);
            return await _userManager.VerifyTwoFactorTokenAsync(appUser, _userManager.Options.Tokens.AuthenticatorTokenProvider, token);
        }

        public async Task<AuthResult> TwoFactorSignInAsync(string provider, string code, bool rememberMe, bool rememberMachine)
            => (await _signInManager.TwoFactorSignInAsync(provider, code, rememberMe, rememberMachine)).ToAuthResult();

        // ── Cookie Refresh ────────────────────────────────────────────────────────

        public async Task RefreshSignInAsync(User user)
            => await _signInManager.RefreshSignInAsync(await RequireAppUserAsync(user));

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Core only ever holds the plain domain User, so every Identity-native operation
        /// re-resolves the real ApplicationUser by Id first. One extra lookup per call — the
        /// cost of keeping Identity out of Core.
        /// </summary>
        private async Task<ApplicationUser> RequireAppUserAsync(User user)
            => await _userManager.FindByIdAsync(user.Id.ToString())
                ?? throw new InvalidOperationException($"User '{user.Id}' was not found.");
    }
}
