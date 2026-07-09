using Microsoft.AspNetCore.Identity;
using SkyScan.Core.Entities;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Infrastructure.Identity;
using System;
using System.Security.Claims;
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
        {
            var appUser = await _userManager.FindByEmailAsync(email);
            if (appUser != null && appUser.IsDeleted)
            {
                return AuthResult.Failed("This account has been deleted.");
            }
            return (await _signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: true)).ToAuthResult();
        }

        public async Task LogoutUserAsync()
            => await _signInManager.SignOutAsync();

        public async Task<User?> GetUserByEmailAsync(string email)
            => (await _userManager.FindByEmailAsync(email))?.ToDomain();

        public async Task<User?> GetUserByIdAsync(Guid id)
            => (await _userManager.FindByIdAsync(id.ToString()))?.ToDomain();

        public async Task<User?> GetCurrentUserAsync(ClaimsPrincipal principal)
            => (await _userManager.GetUserAsync(principal))?.ToDomain();

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

        public async Task<AuthResult> ChangePasswordAsync(User user, string oldPassword, string newPassword)
        {
            var appUser = await RequireAppUserAsync(user);
            var result = await _userManager.ChangePasswordAsync(appUser, oldPassword, newPassword);
            if (!result.Succeeded) return result.ToAuthResult();

            await _signInManager.RefreshSignInAsync(appUser);
            return AuthResult.Success();
        }

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

        public async Task<User?> GetTwoFactorAuthenticationUserAsync()
            => (await _signInManager.GetTwoFactorAuthenticationUserAsync())?.ToDomain();

        // ── External (Google) Login ───────────────────────────────────────────────

        public async Task<ExternalLoginData> GetExternalLoginInfoAsync()
        {
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null) return ExternalLoginData.NotFound();

            var email = info.Principal.FindFirst(ClaimTypes.Email)?.Value;
            var name = info.Principal.FindFirst(ClaimTypes.Name)?.Value ?? email ?? "User";

            return new ExternalLoginData
            {
                Found = true,
                LoginProvider = info.LoginProvider,
                ProviderKey = info.ProviderKey,
                ProviderDisplayName = info.ProviderDisplayName,
                Email = email,
                Name = name
            };
        }

        public async Task<AuthResult> ExternalLoginSignInAsync(string loginProvider, string providerKey)
            => (await _signInManager.ExternalLoginSignInAsync(loginProvider, providerKey, isPersistent: false)).ToAuthResult();

        public async Task<AuthResult> LinkExternalLoginAsync(User user, string loginProvider, string providerKey, string? providerDisplayName)
        {
            // Deliberately a non-throwing lookup (unlike RequireAppUserAsync) so a resolve
            // failure here surfaces as a graceful sign-in error instead of an unhandled
            // exception hitting GlobalExceptionMiddleware — matches the original controller's
            // explicit "Unable to complete Google sign-in." branch.
            var appUser = await _userManager.FindByIdAsync(user.Id.ToString());
            if (appUser == null) return AuthResult.Failed("Unable to complete Google sign-in.");

            await _userManager.AddLoginAsync(appUser, new UserLoginInfo(loginProvider, providerKey, providerDisplayName));
            await _signInManager.SignInAsync(appUser, isPersistent: false);
            return AuthResult.Success();
        }

        // ── Cookie Refresh ────────────────────────────────────────────────────────

        public async Task RefreshSignInAsync(User user)
            => await _signInManager.RefreshSignInAsync(await RequireAppUserAsync(user));

        // ── Account Deletion ──────────────────────────────────────────────────────

        public async Task<AuthResult> SoftDeleteAccountAsync(User user)
        {
            var appUser = await RequireAppUserAsync(user);
            appUser.IsDeleted = true;
            appUser.DeletedAtUtc = DateTime.UtcNow;

            var logins = await _userManager.GetLoginsAsync(appUser);
            foreach (var login in logins)
            {
                await _userManager.RemoveLoginAsync(appUser, login.LoginProvider, login.ProviderKey);
            }

            var updateResult = await _userManager.UpdateAsync(appUser);
            if (!updateResult.Succeeded) return updateResult.ToAuthResult();

            await _signInManager.SignOutAsync();
            return AuthResult.Success();
        }

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
