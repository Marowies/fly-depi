using FluentValidation;

namespace SkyScan.Application.Account.DeleteAccount
{
    public class DeleteAccountCommandValidator : AbstractValidator<DeleteAccountCommand>
    {
        public DeleteAccountCommandValidator()
        {
            RuleFor(v => v.User).NotNull().WithMessage("User must be provided.");
            RuleFor(v => v.Password).NotEmpty().WithMessage("Current password is required to delete your account.");
        }
    }
}
