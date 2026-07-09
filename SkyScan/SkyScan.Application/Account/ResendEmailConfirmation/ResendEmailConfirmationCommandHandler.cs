using MediatR;
using SkyScan.Application.Account.Common;
using SkyScan.Application.Common.Interfaces;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Core.Services.Interfaces;

namespace SkyScan.Application.Account.ResendEmailConfirmation
{
    public class ResendEmailConfirmationCommandHandler : IRequestHandler<ResendEmailConfirmationCommand>
    {
        private readonly IUserRepository _userRepository;
        private readonly IEmailService _emailService;
        private readonly IUrlBuilder _urlBuilder;

        public ResendEmailConfirmationCommandHandler(IUserRepository userRepository, IEmailService emailService, IUrlBuilder urlBuilder)
        {
            _userRepository = userRepository;
            _emailService = emailService;
            _urlBuilder = urlBuilder;
        }

        public async Task Handle(ResendEmailConfirmationCommand request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetUserByEmailAsync(request.Email);
            if (user == null) return; // never reveal whether the email exists

            var token = await _userRepository.GenerateEmailConfirmationTokenAsync(user);
            var confirmUrl = _urlBuilder.ActionUrl("ConfirmEmail", "Account", new { userId = user.Id.ToString(), token });

            await _emailService.SendEmailAsync(
                user.Email,
                "SkyScan – Confirm Your Email",
                AccountEmailTemplates.BuildConfirmEmailBody(user.Name, confirmUrl));
        }
    }
}
