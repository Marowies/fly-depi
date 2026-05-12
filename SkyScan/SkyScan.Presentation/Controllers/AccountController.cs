using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SkyScan.Core.Entities;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Presentation.Models;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace SkyScan.Presentation.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserRepository   _userRepository;
        private readonly IEmailService     _emailService;
        private readonly UrlEncoder        _urlEncoder;
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;

        public AccountController(
            IUserRepository    userRepository,
            IEmailService      emailService,
            UrlEncoder         urlEncoder,
            UserManager<User>  userManager,
            SignInManager<User> signInManager)
        {
            _userRepository = userRepository;
            _emailService   = emailService;
            _urlEncoder     = urlEncoder;
            _userManager    = userManager;
            _signInManager  = signInManager;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // REGISTER
        // ══════════════════════════════════════════════════════════════════════════

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = new User
            {
                UserName = model.Email,
                Email    = model.Email,
                Name     = model.Name
            };

            var result = await _userRepository.RegisterUserAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }

            // Send email confirmation
            var token = await _userRepository.GenerateEmailConfirmationTokenAsync(user);
            var confirmUrl = Url.Action(
                nameof(ConfirmEmail), "Account",
                new { userId = user.Id.ToString(), token = token },
                protocol: Request.Scheme)!;

            await _emailService.SendEmailAsync(
                user.Email!,
                "SkyScan – Confirm Your Email",
                BuildConfirmEmailBody(user.Name, confirmUrl));

            return RedirectToAction(nameof(RegisterConfirmation));
        }

        [HttpGet]
        public IActionResult RegisterConfirmation() => View();

        // ══════════════════════════════════════════════════════════════════════════
        // EMAIL CONFIRMATION
        // ══════════════════════════════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
                return RedirectToAction(nameof(ConfirmEmailError));

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return RedirectToAction(nameof(ConfirmEmailError));

            var result = await _userRepository.ConfirmEmailAsync(user, token);
            return result.Succeeded
                ? View("ConfirmEmailSuccess")
                : View("ConfirmEmailError");
        }

        [HttpGet]
        public IActionResult ConfirmEmailError() => View();

        [HttpGet]
        public IActionResult ResendEmailConfirmation() => View(new ResendEmailConfirmationViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendEmailConfirmation(ResendEmailConfirmationViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userRepository.GetUserByEmailAsync(model.Email);
            if (user != null)
            {
                var token = await _userRepository.GenerateEmailConfirmationTokenAsync(user);
                var confirmUrl = Url.Action(
                    nameof(ConfirmEmail), "Account",
                    new { userId = user.Id.ToString(), token = token },
                    protocol: Request.Scheme)!;

                await _emailService.SendEmailAsync(
                    user.Email!,
                    "SkyScan – Confirm Your Email",
                    BuildConfirmEmailBody(user.Name, confirmUrl));
            }

            // Always redirect — never reveal whether the email exists
            TempData["Message"] = "If that email is registered, a confirmation link has been sent.";
            return RedirectToAction(nameof(RegisterConfirmation));
        }

        // ══════════════════════════════════════════════════════════════════════════
        // LOGIN
        // ══════════════════════════════════════════════════════════════════════════

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid) return View(model);

            var result = await _userRepository.LoginUserAsync(model.Email, model.Password, model.RememberMe);

            if (result.Succeeded)
                return LocalRedirectOrHome(returnUrl);

            if (result.RequiresTwoFactor)
                return RedirectToAction(nameof(TwoFactorLogin), new { returnUrl, rememberMe = model.RememberMe });

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Account locked out. Please try again later.");
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        // ══════════════════════════════════════════════════════════════════════════
        // LOGOUT
        // ══════════════════════════════════════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _userRepository.LogoutUserAsync();
            return RedirectToAction("Index", "Home");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // FORGOT PASSWORD
        // ══════════════════════════════════════════════════════════════════════════

        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userRepository.GetUserByEmailAsync(model.Email);

            if (user != null)
            {
                var token = await _userRepository.GeneratePasswordResetTokenAsync(user);
                var resetUrl = Url.Action(
                    nameof(ResetPassword), "Account",
                    new { email = user.Email, token = token },
                    protocol: Request.Scheme)!;

                await _emailService.SendEmailAsync(
                    user.Email!,
                    "SkyScan – Reset Your Password",
                    BuildResetPasswordBody(user.Name, resetUrl));
            }

            // Always redirect — never reveal whether email is registered
            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation() => View();

        // ══════════════════════════════════════════════════════════════════════════
        // RESET PASSWORD
        // ══════════════════════════════════════════════════════════════════════════

        [HttpGet]
        public IActionResult ResetPassword(string? email = null, string? token = null)
        {
            if (email == null || token == null)
                return BadRequest("A valid email and token are required.");

            return View(new ResetPasswordViewModel { Email = email, Token = token });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userRepository.GetUserByEmailAsync(model.Email);
            if (user == null)
                return RedirectToAction(nameof(ResetPasswordConfirmation));

            var result = await _userRepository.ResetPasswordAsync(user, model.Token, model.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }

            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        [HttpGet]
        public IActionResult ResetPasswordConfirmation() => View();

        // ══════════════════════════════════════════════════════════════════════════
        // REFRESH COOKIE
        // ══════════════════════════════════════════════════════════════════════════

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RefreshCookie(string? returnUrl = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
                await _userRepository.RefreshSignInAsync(user);

            TempData["Message"] = "Session refreshed successfully.";
            return LocalRedirectOrHome(returnUrl);
        }

        // ══════════════════════════════════════════════════════════════════════════
        // TWO-FACTOR AUTHENTICATION – LOGIN STEP
        // ══════════════════════════════════════════════════════════════════════════

        [HttpGet]
        public IActionResult TwoFactorLogin(string? returnUrl = null, bool rememberMe = false)
        {
            ViewData["ReturnUrl"]  = returnUrl;
            ViewData["RememberMe"] = rememberMe;
            return View(new TwoFactorVerifyViewModel { RememberMe = rememberMe });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TwoFactorLogin(TwoFactorVerifyViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);

            var code = model.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
            var result = await _userRepository.TwoFactorSignInAsync(
                "Authenticator", code, model.RememberMe, model.RememberMachine);

            if (result.Succeeded)
                return LocalRedirectOrHome(returnUrl);

            ModelState.AddModelError(string.Empty, "Invalid authenticator code.");
            return View(model);
        }

        // ══════════════════════════════════════════════════════════════════════════
        // TWO-FACTOR AUTHENTICATION – SETUP (Manage)
        // ══════════════════════════════════════════════════════════════════════════

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> EnableTwoFactor()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var key = await _userRepository.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(key))
            {
                await _userRepository.ResetAuthenticatorKeyAsync(user);
                key = await _userRepository.GetAuthenticatorKeyAsync(user);
            }

            var formattedKey = FormatKey(key!);
            var authenticatorUri = GenerateQrCodeUri(user.Email!, key!);

            return View(new EnableTwoFactorViewModel
            {
                SharedKey        = formattedKey,
                AuthenticatorUri = authenticatorUri
            });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnableTwoFactor(EnableTwoFactorViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (!ModelState.IsValid)
            {
                var key = await _userRepository.GetAuthenticatorKeyAsync(user);
                model.SharedKey        = FormatKey(key ?? "");
                model.AuthenticatorUri = GenerateQrCodeUri(user.Email!, key ?? "");
                return View(model);
            }

            var code = model.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
            var isValid = await _userRepository.VerifyTwoFactorTokenAsync(user, code);

            if (!isValid)
            {
                ModelState.AddModelError(string.Empty, "Verification code is invalid.");
                var key = await _userRepository.GetAuthenticatorKeyAsync(user);
                model.SharedKey        = FormatKey(key ?? "");
                model.AuthenticatorUri = GenerateQrCodeUri(user.Email!, key ?? "");
                return View(model);
            }

            await _userRepository.SetTwoFactorEnabledAsync(user, true);
            TempData["Message"] = "Two-factor authentication has been enabled.";
            return RedirectToAction(nameof(TwoFactorEnabled));
        }

        [HttpGet]
        [Authorize]
        public IActionResult TwoFactorEnabled() => View();

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisableTwoFactor()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            await _userRepository.SetTwoFactorEnabledAsync(user, false);
            TempData["Message"] = "Two-factor authentication has been disabled.";
            return RedirectToAction("Index", "Home");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // GOOGLE EXTERNAL LOGIN
        // ══════════════════════════════════════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GoogleLogin(string? returnUrl = null)
        {
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
            var properties  = _signInManager.ConfigureExternalAuthenticationProperties("Google", redirectUrl);
            return Challenge(properties, "Google");
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null)
        {
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                TempData["Error"] = "External login failed. Please try again.";
                return RedirectToAction(nameof(Login));
            }

            // Sign in if external login already linked
            var signInResult = await _signInManager.ExternalLoginSignInAsync(
                info.LoginProvider, info.ProviderKey, isPersistent: false);

            if (signInResult.Succeeded)
                return LocalRedirectOrHome(returnUrl);

            // Create a new account linked to Google
            var email = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            var name  = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? email ?? "User";

            if (email != null)
            {
                var user = await _userRepository.GetUserByEmailAsync(email);
                if (user == null)
                {
                    user = new User
                    {
                        UserName       = email,
                        Email          = email,
                        Name           = name,
                        EmailConfirmed = true   // Google emails are already verified
                    };
                    var createResult = await _userRepository.RegisterUserAsync(user, Guid.NewGuid().ToString() + "Aa1!");
                    if (!createResult.Succeeded)
                    {
                        TempData["Error"] = "Unable to create account via Google.";
                        return RedirectToAction(nameof(Login));
                    }
                }

                await _userManager.AddLoginAsync(user, new UserLoginInfo(info.LoginProvider, info.ProviderKey, info.ProviderDisplayName));
                await _signInManager.SignInAsync(user, isPersistent: false);
                return LocalRedirectOrHome(returnUrl);
            }

            TempData["Error"] = "Could not retrieve email from Google. Please try again.";
            return RedirectToAction(nameof(Login));
        }

        // ══════════════════════════════════════════════════════════════════════════
        // ACCESS DENIED
        // ══════════════════════════════════════════════════════════════════════════

        [HttpGet]
        public IActionResult AccessDenied() => View();

        // ══════════════════════════════════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════════════════════════════════

        private IActionResult LocalRedirectOrHome(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }

        private static string FormatKey(string unformattedKey)
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
            return result.ToString().ToLowerInvariant();
        }

        private string GenerateQrCodeUri(string email, string unformattedKey)
        {
            const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";
            return string.Format(
                AuthenticatorUriFormat,
                _urlEncoder.Encode("SkyScan"),
                _urlEncoder.Encode(email),
                unformattedKey);
        }

        // ── Email template helpers ────────────────────────────────────────────────

        private static string BuildConfirmEmailBody(string name, string confirmUrl) => $@"
        <div style=""font-family:Manrope,Inter,sans-serif;background:#0b1229;color:#dce1ff;padding:40px;border-radius:16px;max-width:520px;margin:auto"">
          <h2 style=""color:#bfc5e4;margin-bottom:8px"">Welcome to SkyScan, {System.Net.WebUtility.HtmlEncode(name)}!</h2>
          <p style=""color:#bfc5e4cc;line-height:1.6"">Please confirm your email address by clicking the button below.</p>
          <a href=""{confirmUrl}"" style=""display:inline-block;margin:24px 0;padding:14px 32px;background:#bfc5e4;color:#0b1229;font-weight:700;border-radius:12px;text-decoration:none;letter-spacing:.05em"">
            Confirm Email
          </a>
          <p style=""font-size:12px;color:#bfc5e4aa"">If you did not create a SkyScan account, please ignore this email.</p>
        </div>";

        private static string BuildResetPasswordBody(string name, string resetUrl) => $@"
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
