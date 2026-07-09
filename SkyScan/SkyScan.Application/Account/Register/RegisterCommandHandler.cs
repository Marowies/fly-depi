using MediatR;
using SkyScan.Application.Account.Common;
using SkyScan.Application.Common.Interfaces;
using SkyScan.Core.Entities;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Core.Services.Interfaces;

namespace SkyScan.Application.Account.Register
{
    public class RegisterCommandHandler : IRequestHandler<RegisterCommand, RegisterResult>
    {
        private readonly IUserRepository _userRepository;
        private readonly IEmailService _emailService;
        private readonly IUrlBuilder _urlBuilder;

        public RegisterCommandHandler(IUserRepository userRepository, IEmailService emailService, IUrlBuilder urlBuilder)
        {
            _userRepository = userRepository;
            _emailService = emailService;
            _urlBuilder = urlBuilder;
        }

        public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
        {
            var user = new User { Email = request.Email, Name = request.Name };

            var result = await _userRepository.RegisterUserAsync(user, request.Password);
            if (!result.Succeeded)
                return new RegisterResult { Success = false, Errors = result.Errors.ToList() };

            var token = await _userRepository.GenerateEmailConfirmationTokenAsync(user);
            var confirmUrl = _urlBuilder.ActionUrl("ConfirmEmail", "Account", new { userId = user.Id.ToString(), token });

            await _emailService.SendEmailAsync(
                user.Email,
                "SkyScan – Confirm Your Email",
                AccountEmailTemplates.BuildConfirmEmailBody(user.Name, confirmUrl));

            return new RegisterResult { Success = true };
        }
    }
}
