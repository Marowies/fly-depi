using MediatR;

namespace SkyScan.Application.Account.TwoFactorLogin
{
    public class TwoFactorLoginStatusQuery : IRequest<TwoFactorLoginStatusResult>
    {
    }

    public class TwoFactorLoginStatusResult
    {
        public bool HasPendingUser { get; set; }
    }
}
