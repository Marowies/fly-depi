using MediatR;
using SkyScan.Core.Entities;
using SkyScan.Core.Repositories_Interfaces;

namespace SkyScan.Application.Account.ExternalLoginCallback
{
    public class ExternalLoginCallbackCommandHandler : IRequestHandler<ExternalLoginCallbackCommand, ExternalLoginCallbackResult>
    {
        private readonly IUserRepository _userRepository;

        public ExternalLoginCallbackCommandHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<ExternalLoginCallbackResult> Handle(ExternalLoginCallbackCommand request, CancellationToken cancellationToken)
        {
            var info = await _userRepository.GetExternalLoginInfoAsync();
            if (!info.Found)
                return new ExternalLoginCallbackResult { Success = false, ErrorMessage = "External login failed. Please try again." };

            // Sign in if this external login is already linked to an account.
            var signInResult = await _userRepository.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey);
            if (signInResult.Succeeded)
                return new ExternalLoginCallbackResult { Success = true, ReturnUrl = request.ReturnUrl };

            if (info.Email == null)
                return new ExternalLoginCallbackResult { Success = false, ErrorMessage = "Could not retrieve email from Google. Please try again." };

            // Create a new account linked to Google if one doesn't already exist for this email.
            var user = await _userRepository.GetUserByEmailAsync(info.Email);
            if (user == null)
            {
                user = new User
                {
                    Email = info.Email,
                    Name = info.Name ?? info.Email,
                    EmailConfirmed = true // Google emails are already verified
                };

                var createResult = await _userRepository.RegisterUserAsync(user, Guid.NewGuid().ToString() + "Aa1!");
                if (!createResult.Succeeded)
                    return new ExternalLoginCallbackResult { Success = false, ErrorMessage = "Unable to create account via Google." };
            }

            var linkResult = await _userRepository.LinkExternalLoginAsync(user, info.LoginProvider, info.ProviderKey, info.ProviderDisplayName);
            if (!linkResult.Succeeded)
                return new ExternalLoginCallbackResult { Success = false, ErrorMessage = linkResult.Errors.FirstOrDefault() ?? "Unable to complete Google sign-in." };

            return new ExternalLoginCallbackResult { Success = true, ReturnUrl = request.ReturnUrl };
        }
    }
}
