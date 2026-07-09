using MediatR;
using SkyScan.Core.Entities;

namespace SkyScan.Application.Account.GetUserProfile
{
    public class GetUserProfileQuery : IRequest<UserProfileResult>
    {
        public Guid UserId { get; set; }
    }

    public class UserProfileResult
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool TwoFactorEnabled { get; set; }
        public List<Search> Searches { get; set; } = new();
        public List<PriceAlert> PriceAlerts { get; set; } = new();
    }
}
