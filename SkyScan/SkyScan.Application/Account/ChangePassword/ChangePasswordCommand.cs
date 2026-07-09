using MediatR;

namespace SkyScan.Application.Account.ChangePassword
{
    public class ChangePasswordCommand : IRequest<ChangePasswordResult>
    {
        public Guid UserId { get; set; }
        public string OldPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class ChangePasswordResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
