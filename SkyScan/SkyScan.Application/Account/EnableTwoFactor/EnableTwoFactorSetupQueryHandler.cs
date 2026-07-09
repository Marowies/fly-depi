using MediatR;
using SkyScan.Application.Account.Common;
using SkyScan.Core.Repositories_Interfaces;
using System.Text.Encodings.Web;

namespace SkyScan.Application.Account.EnableTwoFactor
{
    public class EnableTwoFactorSetupQueryHandler : IRequestHandler<EnableTwoFactorSetupQuery, EnableTwoFactorSetupResult>
    {
        private readonly IUserRepository _userRepository;
        private readonly UrlEncoder _urlEncoder;

        public EnableTwoFactorSetupQueryHandler(IUserRepository userRepository, UrlEncoder urlEncoder)
        {
            _userRepository = userRepository;
            _urlEncoder = urlEncoder;
        }

        public async Task<EnableTwoFactorSetupResult> Handle(EnableTwoFactorSetupQuery request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetUserByIdAsync(request.UserId);
            if (user == null) return new EnableTwoFactorSetupResult();

            return await EnableTwoFactorHelper.BuildAsync(_userRepository, _urlEncoder, user, request.InitializeIfMissing);
        }
    }
}
