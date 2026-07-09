using MediatR;
using SkyScan.Core.Repositories_Interfaces;

namespace SkyScan.Application.Account.GetUserProfile
{
    /// <summary>
    /// Replaces AccountController.Profile()'s direct SkyScanDbContext + multi-level
    /// ThenInclude query (Phase 0 Finding #1) with two existing, already-scoped repository
    /// methods that return exactly the same eagerly-loaded shape the view needs — no new
    /// repository methods required, since ISearchRepository.GetRecentSearchesByUserIdAsync
    /// and IPriceAlertRepository.GetPriceAlertsByUserIdAsync already include everything
    /// Profile.cshtml reads (OriginCity/DestinationCity; Trip→Flights→Airline/Airports→City).
    /// </summary>
    public class GetUserProfileQueryHandler : IRequestHandler<GetUserProfileQuery, UserProfileResult>
    {
        private readonly IUserRepository _userRepository;
        private readonly ISearchRepository _searchRepository;
        private readonly IPriceAlertRepository _priceAlertRepository;

        public GetUserProfileQueryHandler(
            IUserRepository userRepository,
            ISearchRepository searchRepository,
            IPriceAlertRepository priceAlertRepository)
        {
            _userRepository = userRepository;
            _searchRepository = searchRepository;
            _priceAlertRepository = priceAlertRepository;
        }

        public async Task<UserProfileResult> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetUserByIdAsync(request.UserId);
            if (user == null) return new UserProfileResult();

            var twoFactorEnabled = await _userRepository.GetTwoFactorEnabledAsync(user);

            // Profile.cshtml's own OrderByDescending(TimeStamp).Take(10) made the view's
            // effective display limit 10 already; requesting count: 10 here just moves that
            // limit into the query instead of loading the user's full history unfiltered.
            var searches = await _searchRepository.GetRecentSearchesByUserIdAsync(user.Id, count: 10);
            var priceAlerts = await _priceAlertRepository.GetPriceAlertsByUserIdAsync(user.Id);

            return new UserProfileResult
            {
                Name = user.Name,
                Email = user.Email,
                TwoFactorEnabled = twoFactorEnabled,
                Searches = searches.ToList(),
                PriceAlerts = priceAlerts.ToList()
            };
        }
    }
}
