using Microsoft.EntityFrameworkCore;
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
    }
}
