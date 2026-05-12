using System.ComponentModel.DataAnnotations;

namespace SkyScan.Presentation.Models
{
    // ── Forgot Password ──────────────────────────────────────────────────────────
    public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
    }

    // ── Reset Password ───────────────────────────────────────────────────────────
    public class ResetPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    // ── Two-Factor Authentication ────────────────────────────────────────────────
    public class TwoFactorVerifyViewModel
    {
        [Required]
        [StringLength(7, MinimumLength = 6, ErrorMessage = "Code must be 6 digits.")]
        [DataType(DataType.Text)]
        [Display(Name = "Authenticator Code")]
        public string Code { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
        public bool RememberMachine { get; set; }
    }

    public class EnableTwoFactorViewModel
    {
        public string? SharedKey { get; set; }
        public string? AuthenticatorUri { get; set; }

        [Required]
        [StringLength(7, MinimumLength = 6, ErrorMessage = "Code must be 6 digits.")]
        [DataType(DataType.Text)]
        [Display(Name = "Verification Code")]
        public string Code { get; set; } = string.Empty;
    }

    // ── Email Confirmation Resend ─────────────────────────────────────────────────
    public class ResendEmailConfirmationViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
