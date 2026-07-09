using MediatR;

namespace SkyScan.Application.Account.EnableTwoFactor
{
    public class EnableTwoFactorCommand : IRequest<EnableTwoFactorCommandResult>
    {
        public Guid UserId { get; set; }
        public string Code { get; set; } = string.Empty;
    }

    public class EnableTwoFactorCommandResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string SharedKey { get; set; } = string.Empty;
        public string AuthenticatorUri { get; set; } = string.Empty;
    }
}
