using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SkyScan.Application.Account.ChangePassword;
using SkyScan.Application.Account.ConfirmEmail;
using SkyScan.Application.Account.DisableTwoFactor;
using SkyScan.Application.Account.EnableTwoFactor;
using SkyScan.Application.Account.ExternalLoginCallback;
using SkyScan.Application.Account.ForgotPassword;
using SkyScan.Application.Account.GetUserProfile;
using SkyScan.Application.Account.Login;
using SkyScan.Application.Account.Register;
using SkyScan.Application.Account.ResendEmailConfirmation;
using SkyScan.Application.Account.ResetPassword;
using SkyScan.Application.Account.TwoFactorLogin;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Infrastructure.Identity;
using SkyScan.Presentation.Models;

namespace SkyScan.Presentation.Controllers
{
    public class AccountController : Controller
    {
        private readonly IMediator _mediator;
        private readonly IUserRepository _userRepository;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AccountController(IMediator mediator, IUserRepository userRepository, SignInManager<ApplicationUser> signInManager)
        {
            _mediator = mediator;
            _userRepository = userRepository;
            _signInManager = signInManager;
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

            var result = await _mediator.Send(new RegisterCommand { Name = model.Name, Email = model.Email, Password = model.Password });
            if (!result.Success)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error);
                return View(model);
            }

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
            var result = await _mediator.Send(new ConfirmEmailCommand { UserId = userId, Token = token });
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

            await _mediator.Send(new ResendEmailConfirmationCommand { Email = model.Email });

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

            var result = await _mediator.Send(new LoginCommand { Email = model.Email, Password = model.Password, RememberMe = model.RememberMe });

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

            await _mediator.Send(new ForgotPasswordCommand { Email = model.Email });

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

            var result = await _mediator.Send(new ResetPasswordCommand { Email = model.Email, Token = model.Token, Password = model.Password });
            if (!result.UserFound)
                return RedirectToAction(nameof(ResetPasswordConfirmation));

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error);
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
            var currentUser = await _userRepository.GetCurrentUserAsync(User);
            if (currentUser != null)
                await _userRepository.RefreshSignInAsync(currentUser);

            TempData["Message"] = "Session refreshed successfully.";
            return LocalRedirectOrHome(returnUrl);
        }

        // ══════════════════════════════════════════════════════════════════════════
        // TWO-FACTOR AUTHENTICATION – LOGIN STEP
        // ══════════════════════════════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> TwoFactorLogin(string? returnUrl = null, bool rememberMe = false)
        {
            var status = await _mediator.Send(new TwoFactorLoginStatusQuery());
            if (!status.HasPendingUser)
            {
                return RedirectToAction(nameof(Login));
            }
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["RememberMe"] = rememberMe;
            return View(new TwoFactorVerifyViewModel { RememberMe = rememberMe });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TwoFactorLogin(TwoFactorVerifyViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);

            var result = await _mediator.Send(new TwoFactorLoginCommand
            {
                Code = model.Code,
                RememberMe = model.RememberMe,
                RememberMachine = model.RememberMachine
            });

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
            var currentUser = await _userRepository.GetCurrentUserAsync(User);
            if (currentUser == null) return Challenge();

            var result = await _mediator.Send(new EnableTwoFactorSetupQuery { UserId = currentUser.Id, InitializeIfMissing = true });

            return View(new EnableTwoFactorViewModel
            {
                SharedKey = result.SharedKey,
                AuthenticatorUri = result.AuthenticatorUri
            });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnableTwoFactor(EnableTwoFactorViewModel model)
        {
            var currentUser = await _userRepository.GetCurrentUserAsync(User);
            if (currentUser == null) return Challenge();

            if (!ModelState.IsValid)
            {
                var setup = await _mediator.Send(new EnableTwoFactorSetupQuery { UserId = currentUser.Id, InitializeIfMissing = false });
                model.SharedKey = setup.SharedKey;
                model.AuthenticatorUri = setup.AuthenticatorUri;
                return View(model);
            }

            var result = await _mediator.Send(new EnableTwoFactorCommand { UserId = currentUser.Id, Code = model.Code });
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Verification code is invalid.");
                model.SharedKey = result.SharedKey;
                model.AuthenticatorUri = result.AuthenticatorUri;
                return View(model);
            }

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
            var currentUser = await _userRepository.GetCurrentUserAsync(User);
            if (currentUser == null) return Challenge();

            await _mediator.Send(new DisableTwoFactorCommand { UserId = currentUser.Id });
            TempData["Message"] = "Two-factor authentication has been disabled.";
            return RedirectToAction("Index", "Home");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // GOOGLE EXTERNAL LOGIN
        // ══════════════════════════════════════════════════════════════════════════

        // Not routed through IUserRepository/MediatR: this action's only job is to build an
        // ASP.NET Core AuthenticationProperties payload and return a Challenge() result —
        // both are framework-native constructs a MediatR handler cannot return (handlers
        // return data, not IActionResult). See Phase 2d report Flag F1.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GoogleLogin(string? returnUrl = null)
        {
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties("Google", redirectUrl);
            return Challenge(properties, "Google");
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null)
        {
            var result = await _mediator.Send(new ExternalLoginCallbackCommand { ReturnUrl = returnUrl });
            if (!result.Success)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction(nameof(Login));
            }

            return LocalRedirectOrHome(result.ReturnUrl);
        }

        // ══════════════════════════════════════════════════════════════════════════
        // CHANGE PASSWORD
        // ══════════════════════════════════════════════════════════════════════════

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["PasswordError"] = "Please fill in all fields correctly.";
                return RedirectToAction(nameof(Profile));
            }

            var currentUser = await _userRepository.GetCurrentUserAsync(User);
            if (currentUser == null) return Challenge();

            var result = await _mediator.Send(new ChangePasswordCommand
            {
                UserId = currentUser.Id,
                OldPassword = model.OldPassword,
                NewPassword = model.NewPassword
            });

            if (!result.Success)
            {
                TempData["PasswordError"] = result.ErrorMessage;
                return RedirectToAction(nameof(Profile));
            }

            TempData["PasswordSuccess"] = "Your password has been changed successfully.";
            return RedirectToAction(nameof(Profile));
        }

        // ══════════════════════════════════════════════════════════════════════════
        // PROFILE
        // ══════════════════════════════════════════════════════════════════════════

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var currentUser = await _userRepository.GetCurrentUserAsync(User);
            if (currentUser == null) return Challenge();

            var result = await _mediator.Send(new GetUserProfileQuery { UserId = currentUser.Id });
            return View(result);
        }

        // ══════════════════════════════════════════════════════════════════════════
        // DELETE ACCOUNT
        // ══════════════════════════════════════════════════════════════════════════

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("BookingPolicy")]
        public async Task<IActionResult> DeleteAccount(DeleteAccountViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["DeleteError"] = "Current password is required.";
                return RedirectToAction(nameof(Profile));
            }

            var currentUser = await _userRepository.GetCurrentUserAsync(User);
            if (currentUser == null) return Challenge();

            var result = await _mediator.Send(new SkyScan.Application.Account.DeleteAccount.DeleteAccountCommand 
            { 
                User = currentUser, 
                Password = model.Password 
            });

            if (!result.Succeeded)
            {
                TempData["DeleteError"] = result.Errors.FirstOrDefault() ?? "Unable to delete account.";
                return RedirectToAction(nameof(Profile));
            }

            TempData["Message"] = "Your account has been deleted.";
            return RedirectToAction("Index", "Home");
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
    }
}
