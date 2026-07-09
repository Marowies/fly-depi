using MediatR;

namespace SkyScan.Application.Account.TwoFactorLogin
{
    public class TwoFactorLoginCommand : IRequest<TwoFactorLoginCommandResult>
    {
        public string Code { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
        public bool RememberMachine { get; set; }
    }

    public class TwoFactorLoginCommandResult
    {
        public bool Succeeded { get; set; }
    }
}
