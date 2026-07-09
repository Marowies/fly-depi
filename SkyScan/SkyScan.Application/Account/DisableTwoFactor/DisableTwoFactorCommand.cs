using MediatR;

namespace SkyScan.Application.Account.DisableTwoFactor
{
    public class DisableTwoFactorCommand : IRequest
    {
        public Guid UserId { get; set; }
    }
}
