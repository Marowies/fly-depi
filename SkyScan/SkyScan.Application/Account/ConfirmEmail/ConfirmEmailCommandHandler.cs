using MediatR;
using SkyScan.Core.Repositories_Interfaces;

namespace SkyScan.Application.Account.ConfirmEmail
{
    public class ConfirmEmailCommandHandler : IRequestHandler<ConfirmEmailCommand, ConfirmEmailResult>
    {
        private readonly IUserRepository _userRepository;

        public ConfirmEmailCommandHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<ConfirmEmailResult> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.Token))
                return new ConfirmEmailResult { Succeeded = false };

            if (!Guid.TryParse(request.UserId, out var userId))
                return new ConfirmEmailResult { Succeeded = false };

            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
                return new ConfirmEmailResult { Succeeded = false };

            var result = await _userRepository.ConfirmEmailAsync(user, request.Token);
            return new ConfirmEmailResult { Succeeded = result.Succeeded };
        }
    }
}
