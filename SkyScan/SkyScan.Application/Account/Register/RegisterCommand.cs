using MediatR;

namespace SkyScan.Application.Account.Register
{
    public class RegisterCommand : IRequest<RegisterResult>
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterResult
    {
        public bool Success { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
