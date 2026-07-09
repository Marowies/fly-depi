using MediatR;

namespace SkyScan.Application.Account.ResendEmailConfirmation
{
    public class ResendEmailConfirmationCommand : IRequest
    {
        public string Email { get; set; } = string.Empty;
    }
}
