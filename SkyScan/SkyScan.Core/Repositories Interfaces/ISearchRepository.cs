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
    }
}
