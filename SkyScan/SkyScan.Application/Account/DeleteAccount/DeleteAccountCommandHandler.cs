using MediatR;
using SkyScan.Core.Entities;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Core.Services.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SkyScan.Application.Account.DeleteAccount
{
    public class DeleteAccountCommandHandler : IRequestHandler<DeleteAccountCommand, AuthResult>
    {
        private readonly IUserRepository _userRepository;
        private readonly IPriceAlertRepository _priceAlertRepository;
        private readonly ISearchRepository _searchRepository;
        private readonly IEmailService _emailService;

        public DeleteAccountCommandHandler(
            IUserRepository userRepository,
            IPriceAlertRepository priceAlertRepository,
            ISearchRepository searchRepository,
            IEmailService emailService)
        {
            _userRepository = userRepository;
            _priceAlertRepository = priceAlertRepository;
            _searchRepository = searchRepository;
            _emailService = emailService;
        }

        public async Task<AuthResult> Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
        {
            // 1. Verify Password
            // Note: We use LoginUserAsync as a workaround to verify the password since 
            // IUserRepository doesn't have a direct CheckPassword method.
            var loginResult = await _userRepository.LoginUserAsync(request.User.Email, request.Password, false);
            if (!loginResult.Succeeded)
            {
                return AuthResult.Failed("Incorrect password.");
            }

            // 2. Cascade Deletes / Anonymization
            // Wrapped in a try-finally if we had a transaction, but EF Core context
            // might not be shared across these distinct repositories if they are scoped differently,
            // or we'd need IUnitOfWork. Assuming standard DbContext sharing per request, we just call them.
            // (Flagged: No explicit transaction used because IUnitOfWork pattern is not visible here).
            await _priceAlertRepository.DeleteUserAlertsAsync(request.User.Id);
            await _searchRepository.AnonymizeUserSearchesAsync(request.User.Id);

            // 3. Soft Delete Account
            var deleteResult = await _userRepository.SoftDeleteAccountAsync(request.User);
            if (!deleteResult.Succeeded)
            {
                return deleteResult;
            }

            // 4. Send Confirmation Email
            var emailSubject = "Your SkyScan Account Has Been Deleted";
            var emailBody = $@"
                <p>Hello {request.User.Name},</p>
                <p>Your SkyScan account has been successfully deleted.</p>
                <p>Your profile, price alerts, and external logins have been removed. Any remaining booking data will be permanently purged after 30 days.</p>
                <p>If this was a mistake, please contact support immediately.</p>
                <p>Best regards,<br/>The SkyScan Team</p>";

            await _emailService.SendEmailAsync(request.User.Email, emailSubject, emailBody);

            return AuthResult.Success();
        }
    }
}
