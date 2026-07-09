using MediatR;
using SkyScan.Core.Repositories_Interfaces;

namespace SkyScan.Application.Account.ChangePassword
{
    public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, ChangePasswordResult>
    {
        private readonly IUserRepository _userRepository;

        public ChangePasswordCommandHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<ChangePasswordResult> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetUserByIdAsync(request.UserId);
            if (user == null)
                return new ChangePasswordResult { Success = false, ErrorMessage = "User not found." };

            var result = await _userRepository.ChangePasswordAsync(user, request.OldPassword, request.NewPassword);
            return result.Succeeded
                ? new ChangePasswordResult { Success = true }
                : new ChangePasswordResult { Success = false, ErrorMessage = string.Join(" ", result.Errors) };
        }
    }
}
