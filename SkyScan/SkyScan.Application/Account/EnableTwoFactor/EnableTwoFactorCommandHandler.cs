using MediatR;
using SkyScan.Application.Account.Common;
using SkyScan.Core.Repositories_Interfaces;
using System.Text.Encodings.Web;

namespace SkyScan.Application.Account.EnableTwoFactor
{
    public class EnableTwoFactorCommandHandler : IRequestHandler<EnableTwoFactorCommand, EnableTwoFactorCommandResult>
    {
        private readonly IUserRepository _userRepository;
        private readonly UrlEncoder _urlEncoder;

        public EnableTwoFactorCommandHandler(IUserRepository userRepository, UrlEncoder urlEncoder)
        {
            _userRepository = userRepository;
            _urlEncoder = urlEncoder;
        }

        public async Task<EnableTwoFactorCommandResult> Handle(EnableTwoFactorCommand request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetUserByIdAsync(request.UserId);
            if (user == null)
                return new EnableTwoFactorCommandResult { Success = false, ErrorMessage = "User not found." };

            var code = request.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
            var isValid = await _userRepository.VerifyTwoFactorTokenAsync(user, code);

            if (!isValid)
            {
                var redisplay = await EnableTwoFactorHelper.BuildAsync(_userRepository, _urlEncoder, user, initializeIfMissing: false);
                return new EnableTwoFactorCommandResult
                {
                    Success = false,
                    ErrorMessage = "Verification code is invalid.",
                    SharedKey = redisplay.SharedKey,
                    AuthenticatorUri = redisplay.AuthenticatorUri
                };
            }

            await _userRepository.SetTwoFactorEnabledAsync(user, true);
            return new EnableTwoFactorCommandResult { Success = true };
        }
    }
}
