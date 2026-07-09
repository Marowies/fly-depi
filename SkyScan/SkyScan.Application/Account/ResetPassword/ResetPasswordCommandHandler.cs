using MediatR;
using SkyScan.Core.Repositories_Interfaces;

namespace SkyScan.Application.Account.ResetPassword
{
    public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, ResetPasswordResult>
    {
        private readonly IUserRepository _userRepository;

        public ResetPasswordCommandHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<ResetPasswordResult> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetUserByEmailAsync(request.Email);
            if (user == null)
                return new ResetPasswordResult { UserFound = false };

            var result = await _userRepository.ResetPasswordAsync(user, request.Token, request.Password);
            return new ResetPasswordResult
            {
                UserFound = true,
                Succeeded = result.Succeeded,
                Errors = result.Errors.ToList()
            };
        }
    }
}
