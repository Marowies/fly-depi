using MediatR;
using SkyScan.Application.Account.Common;
using SkyScan.Application.Common.Interfaces;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Core.Services.Interfaces;

namespace SkyScan.Application.Account.ForgotPassword
{
    public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand>
    {
        private readonly IUserRepository _userRepository;
        private readonly IEmailService _emailService;
        private readonly IUrlBuilder _urlBuilder;

        public ForgotPasswordCommandHandler(IUserRepository userRepository, IEmailService emailService, IUrlBuilder urlBuilder)
        {
            _userRepository = userRepository;
            _emailService = emailService;
            _urlBuilder = urlBuilder;
        }

        public async Task Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetUserByEmailAsync(request.Email);
            if (user == null) return; // never reveal whether email is registered

            var token = await _userRepository.GeneratePasswordResetTokenAsync(user);
            var resetUrl = _urlBuilder.ActionUrl("ResetPassword", "Account", new { email = user.Email, token });

            await _emailService.SendEmailAsync(
                user.Email,
                "SkyScan – Reset Your Password",
                AccountEmailTemplates.BuildResetPasswordBody(user.Name, resetUrl));
        }
    }
}
