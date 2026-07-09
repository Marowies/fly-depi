using MediatR;

namespace SkyScan.Application.Account.ForgotPassword
{
    public class ForgotPasswordCommand : IRequest
    {
        public string Email { get; set; } = string.Empty;
    }
}
