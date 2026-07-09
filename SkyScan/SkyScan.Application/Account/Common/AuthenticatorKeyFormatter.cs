using System.Text.Encodings.Web;

namespace SkyScan.Application.Account.Common
{
    /// <summary>
    /// Relocated verbatim from AccountController's private FormatKey/GenerateQrCodeUri
    /// helpers. UrlEncoder is a plain BCL type (System.Text.Encodings.Web), not ASP.NET-Core-
    /// specific, so it's safe to depend on directly here.
    /// </summary>
    public static class AuthenticatorKeyFormatter
    {
        private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";

        public static string FormatKey(string unformattedKey)
        {
            var result = new System.Text.StringBuilder();
            int currentPosition = 0;
            while (currentPosition + 4 < unformattedKey.Length)
            {
                result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
                currentPosition += 4;
            }
            if (currentPosition < unformattedKey.Length)
                result.Append(unformattedKey.AsSpan(currentPosition));
            return result.ToString().ToUpperInvariant();
        }

        public static string GenerateQrCodeUri(UrlEncoder urlEncoder, string email, string unformattedKey) =>
            string.Format(
                AuthenticatorUriFormat,
                urlEncoder.Encode("SkyScan"),
                urlEncoder.Encode(email),
                unformattedKey);
    }
}
