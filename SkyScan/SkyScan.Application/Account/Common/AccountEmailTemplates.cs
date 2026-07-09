namespace SkyScan.Application.Account.Common
{
    /// <summary>
    /// Relocated verbatim from AccountController's private BuildConfirmEmailBody/
    /// BuildResetPasswordBody helpers. Pure string templating — no framework dependency —
    /// shared by RegisterCommandHandler, ResendEmailConfirmationCommandHandler, and
    /// ForgotPasswordCommandHandler.
    /// </summary>
    public static class AccountEmailTemplates
    {
        public static string BuildConfirmEmailBody(string name, string confirmUrl) => $@"
        <div style=""font-family:Manrope,Inter,sans-serif;background:#0b1229;color:#dce1ff;padding:40px;border-radius:16px;max-width:520px;margin:auto"">
          <h2 style=""color:#bfc5e4;margin-bottom:8px"">Welcome to SkyScan, {System.Net.WebUtility.HtmlEncode(name)}!</h2>
          <p style=""color:#bfc5e4cc;line-height:1.6"">Please confirm your email address by clicking the button below.</p>
          <a href=""{confirmUrl}"" style=""display:inline-block;margin:24px 0;padding:14px 32px;background:#bfc5e4;color:#0b1229;font-weight:700;border-radius:12px;text-decoration:none;letter-spacing:.05em"">
            Confirm Email
          </a>
          <p style=""font-size:12px;color:#bfc5e4aa"">If you did not create a SkyScan account, please ignore this email.</p>
        </div>";

        public static string BuildResetPasswordBody(string name, string resetUrl) => $@"
        <div style=""font-family:Manrope,Inter,sans-serif;background:#0b1229;color:#dce1ff;padding:40px;border-radius:16px;max-width:520px;margin:auto"">
          <h2 style=""color:#bfc5e4;margin-bottom:8px"">Reset Your Password</h2>
          <p style=""color:#bfc5e4cc;line-height:1.6"">Hi {System.Net.WebUtility.HtmlEncode(name)}, we received a request to reset your SkyScan password.</p>
          <a href=""{resetUrl}"" style=""display:inline-block;margin:24px 0;padding:14px 32px;background:#dac76a;color:#0b1229;font-weight:700;border-radius:12px;text-decoration:none;letter-spacing:.05em"">
            Reset Password
          </a>
          <p style=""font-size:12px;color:#bfc5e4aa"">This link expires in 1 hour. If you didn't request a reset, please ignore this email.</p>
        </div>";
    }
}
