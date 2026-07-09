using MediatR;
using SkyScan.Core.Repositories_Interfaces;

namespace SkyScan.Application.Account.DisableTwoFactor
{
    public class DisableTwoFactorCommandHandler : IRequestHandler<DisableTwoFactorCommand>
    {
        private readonly IUserRepository _userRepository;

        public DisableTwoFactorCommandHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task Handle(DisableTwoFactorCommand request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetUserByIdAsync(request.UserId);
            if (user == null) return;

            await _userRepository.SetTwoFactorEnabledAsync(user, false);
        }
    }
}
