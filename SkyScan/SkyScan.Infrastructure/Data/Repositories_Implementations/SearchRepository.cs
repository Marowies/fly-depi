using Microsoft.EntityFrameworkCore;
using SkyScan.Core.Constants;
using SkyScan.Core.Entities;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Infrastructure.Data.Data_Sources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SkyScan.Infrastructure.Data.Repositories_Implementations
{
    public class SearchRepository : GenericRepository<Search>, ISearchRepository
    {
        public SearchRepository(SkyScanDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Search>> GetRecentSearchesByUserIdAsync(Guid userId, int count = 5)
        {
            return await _dbSet
                .Include(s => s.OriginCity)
                .Include(s => s.DestinationCity)
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.TimeStamp)
                .Take(count)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<IEnumerable<Search>> GetTopTrendingSearchesAsync(int count = 5)
        {
            var topSearchGroup = await _dbSet
                .GroupBy(s => new { s.OriginCityId, s.DestinationCityId })
                .Select(g => new { g.Key.OriginCityId, g.Key.DestinationCityId, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(count)
                .ToListAsync();

            var trendingSearches = new List<Search>();
            foreach(var item in topSearchGroup)
            {
                var search = await _dbSet
                    .Include(s => s.OriginCity)
                    .Include(s => s.DestinationCity)
                    .FirstOrDefaultAsync(s => s.OriginCityId == item.OriginCityId && s.DestinationCityId == item.DestinationCityId);
                if (search != null) trendingSearches.Add(search);
            }
            return trendingSearches;
        }

        public async Task<IEnumerable<Search>> GetTrendingRoutesSinceAsync(DateTime since, int count = 5)
        {
            var topRoutes = await _dbSet
                .Where(s => s.TimeStamp >= since)
                .GroupBy(s => new { s.OriginCityId, s.DestinationCityId })
                .Select(g => new { g.Key.OriginCityId, g.Key.DestinationCityId, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(count)
                .ToListAsync();

            var routes = new List<Search>();
            foreach (var route in topRoutes)
            {
                var search = await _dbSet
                    .Include(s => s.OriginCity)
                    .Include(s => s.DestinationCity)
                    .FirstOrDefaultAsync(s => s.OriginCityId == route.OriginCityId && s.DestinationCityId == route.DestinationCityId);
                if (search != null) routes.Add(search);
            }
            return routes;
        }

        public async Task LogSearchAsync(Guid userId, Guid originCityId, Guid destinationCityId, DateTime departureDate, TripType type, int maxPerUser = 5)
        {
            var existing = await _dbSet.FirstOrDefaultAsync(s =>
                s.UserId == userId && s.OriginCityId == originCityId && s.DestinationCityId == destinationCityId);

            if (existing != null)
            {
                existing.TimeStamp = DateTime.UtcNow;
                existing.DepartureDate = departureDate;
                existing.Type = type;
            }
            else
            {
                _dbSet.Add(new Search
                {
                    TimeStamp = DateTime.UtcNow,
                    Type = type,
                    DepartureDate = departureDate,
                    OriginCityId = originCityId,
                    DestinationCityId = destinationCityId,
                    UserId = userId
                });

                var userSearches = await _dbSet
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.TimeStamp)
                    .ToListAsync();

                if (userSearches.Count >= maxPerUser)
                {
                    _dbSet.RemoveRange(userSearches.Skip(maxPerUser - 1));
                }
            }

            await _context.SaveChangesAsync();
        }
        public async Task AnonymizeUserSearchesAsync(Guid userId)
        {
            var searches = await _dbSet.Where(s => s.UserId == userId).ToListAsync();
            foreach (var search in searches)
            {
                search.UserId = null;
            }
            await _context.SaveChangesAsync();
        }
    }
}
