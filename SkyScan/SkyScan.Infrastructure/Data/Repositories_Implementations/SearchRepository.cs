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

        public async Task<IEnumerable<Tuple<City, City>>> GetTopTrendingSearchesAsync(int count = 5)
        {
            var topSearchGroup = await _dbSet
                .GroupBy(s => new { s.OriginCityId, s.DestinationCityId })
                .Select(g => new { g.Key.OriginCityId, g.Key.DestinationCityId, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(count)
                .ToListAsync();

            List<Tuple<City, City>> routeList = new List<Tuple<City, City>>();
            Tuple<City, City> route;
            foreach (var item in topSearchGroup)
            {
                City originCity = _context.Cities.FirstOrDefault(C => C.CityId == item.OriginCityId);
                City destenationCity = _context.Cities.FirstOrDefault(C => C.CityId == item.DestinationCityId);
                if (originCity != null && destenationCity != null)
                {
                    route = new Tuple<City, City>(originCity, destenationCity);
                    routeList.Add(route);
                }
                else
                {
                    return new List<Tuple<City, City>>();
                }
            }
            return routeList;
        }
    }
}
