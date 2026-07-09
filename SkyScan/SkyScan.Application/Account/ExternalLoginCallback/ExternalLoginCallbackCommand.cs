using MediatR;

namespace SkyScan.Application.Account.ExternalLoginCallback
{
    public class ExternalLoginCallbackCommand : IRequest<ExternalLoginCallbackResult>
    {
        public string? ReturnUrl { get; set; }
    }

    public class ExternalLoginCallbackResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ReturnUrl { get; set; }
    }
}
