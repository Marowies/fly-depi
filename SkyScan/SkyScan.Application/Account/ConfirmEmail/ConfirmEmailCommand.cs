using MediatR;

namespace SkyScan.Application.Account.ConfirmEmail
{
    public class ConfirmEmailCommand : IRequest<ConfirmEmailResult>
    {
        public string UserId { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }

    public class ConfirmEmailResult
    {
        public bool Succeeded { get; set; }
    }
}
