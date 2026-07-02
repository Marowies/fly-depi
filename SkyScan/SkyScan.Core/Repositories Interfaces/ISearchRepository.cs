using SkyScan.Core.Constants;
using SkyScan.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SkyScan.Core.Repositories_Interfaces
{
    public interface ISearchRepository : IGenericRepository<Search>
    {
        Task<IEnumerable<Search>> GetRecentSearchesByUserIdAsync(Guid userId, int count = 5);
        Task<IEnumerable<Search>> GetTopTrendingSearchesAsync(int count = 5);
        Task<IEnumerable<Search>> GetTrendingRoutesSinceAsync(DateTime since, int count = 5);

        /// <summary>Upserts the user's search for this route (refreshing TimeStamp/date/type if it already exists) and evicts the oldest beyond <paramref name="maxPerUser"/> routes.</summary>
        Task LogSearchAsync(Guid userId, Guid originCityId, Guid destinationCityId, DateTime departureDate, TripType type, int maxPerUser = 5);
    }
}
