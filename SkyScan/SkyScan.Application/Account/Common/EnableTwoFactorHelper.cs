using SkyScan.Core.Entities;
using SkyScan.Core.Repositories_Interfaces;
using System.Text.Encodings.Web;

namespace SkyScan.Application.Account.Common
{
    /// <summary>
    /// Shared by EnableTwoFactorSetupQueryHandler (GET) and EnableTwoFactorCommandHandler
    /// (POST redisplay branches) so the "fetch/format the authenticator key" logic exists
    /// exactly once. Plain static helper, not a MediatR request, matching the
    /// AirportDropdownCache precedent (Phase 2c) for logic shared across multiple handlers.
    /// </summary>
    public static class EnableTwoFactorHelper
    {
        public static async Task<EnableTwoFactorSetupResult> BuildAsync(
            IUserRepository userRepository, UrlEncoder urlEncoder, User user, bool initializeIfMissing)
        {
            var key = await userRepository.GetAuthenticatorKeyAsync(user);

            // Only the GET action initializes a missing key; POST redisplay branches
            // (ModelState invalid, code invalid) just re-fetch whatever key already exists —
            // matches the original controller's two distinct code paths exactly.
            if (initializeIfMissing && string.IsNullOrEmpty(key))
            {
                await userRepository.ResetAuthenticatorKeyAsync(user);
                key = await userRepository.GetAuthenticatorKeyAsync(user);
            }

            return new EnableTwoFactorSetupResult
            {
                SharedKey = AuthenticatorKeyFormatter.FormatKey(key ?? ""),
                AuthenticatorUri = AuthenticatorKeyFormatter.GenerateQrCodeUri(urlEncoder, user.Email, key ?? "")
            };
        }
    }
}
