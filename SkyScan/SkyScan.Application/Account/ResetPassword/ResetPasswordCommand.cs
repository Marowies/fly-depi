using MediatR;

namespace SkyScan.Application.Account.ResetPassword
{
    public class ResetPasswordCommand : IRequest<ResetPasswordResult>
    {
        public string Email { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class ResetPasswordResult
    {
        public bool UserFound { get; set; }
        public bool Succeeded { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
