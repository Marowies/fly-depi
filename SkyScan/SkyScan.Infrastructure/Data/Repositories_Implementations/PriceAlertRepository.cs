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
    public class PriceAlertRepository : GenericRepository<PriceAlert>, IPriceAlertRepository
    {
        public PriceAlertRepository(SkyScanDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<PriceAlert>> GetPriceAlertsByUserIdAsync(Guid userId)
        {
            return await _dbSet
                .Include(p => p.Trip)
                .Where(p => p.UserId == userId)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<IEnumerable<PriceAlert>> GetAlertsTriggeredByPriceAsync(Guid tripId, decimal newPrice)
        {
            return await _dbSet
                .Include(p => p.User)
                .Include(p => p.Trip)
                .Where(p => p.TripId == tripId && p.TargetPrice >= newPrice)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
