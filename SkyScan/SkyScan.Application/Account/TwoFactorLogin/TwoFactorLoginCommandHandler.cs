using MediatR;
using SkyScan.Core.Repositories_Interfaces;

namespace SkyScan.Application.Account.TwoFactorLogin
{
    public class TwoFactorLoginCommandHandler : IRequestHandler<TwoFactorLoginCommand, TwoFactorLoginCommandResult>
    {
        private readonly IUserRepository _userRepository;

        public TwoFactorLoginCommandHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<TwoFactorLoginCommandResult> Handle(TwoFactorLoginCommand request, CancellationToken cancellationToken)
        {
            var code = request.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
            var result = await _userRepository.TwoFactorSignInAsync("Authenticator", code, request.RememberMe, request.RememberMachine);

            return new TwoFactorLoginCommandResult { Succeeded = result.Succeeded };
        }
    }
}
