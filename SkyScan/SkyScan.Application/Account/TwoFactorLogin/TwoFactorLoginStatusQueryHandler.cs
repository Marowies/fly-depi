using MediatR;
using SkyScan.Core.Repositories_Interfaces;

namespace SkyScan.Application.Account.TwoFactorLogin
{
    public class TwoFactorLoginStatusQueryHandler : IRequestHandler<TwoFactorLoginStatusQuery, TwoFactorLoginStatusResult>
    {
        private readonly IUserRepository _userRepository;

        public TwoFactorLoginStatusQueryHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<TwoFactorLoginStatusResult> Handle(TwoFactorLoginStatusQuery request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetTwoFactorAuthenticationUserAsync();
            return new TwoFactorLoginStatusResult { HasPendingUser = user != null };
        }
    }
}
