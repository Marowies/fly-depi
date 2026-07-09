using MediatR;
using SkyScan.Application.Account.Common;

namespace SkyScan.Application.Account.EnableTwoFactor
{
    /// <summary>GET load (InitializeIfMissing = true) and POST ModelState-invalid redisplay
    /// (InitializeIfMissing = false) both go through this one query — see EnableTwoFactorHelper
    /// for why the flag exists instead of two near-duplicate query types.</summary>
    public class EnableTwoFactorSetupQuery : IRequest<EnableTwoFactorSetupResult>
    {
        public Guid UserId { get; set; }
        public bool InitializeIfMissing { get; set; }
    }
}
