using SkyScan.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SkyScan.Core.Repositories_Interfaces
{
    public interface IPriceAlertRepository : IGenericRepository<PriceAlert>
    {
        Task<IEnumerable<PriceAlert>> GetPriceAlertsByUserIdAsync(Guid userId);
        Task<IEnumerable<PriceAlert>> GetAlertsTriggeredByPriceAsync(Guid tripId, decimal newPrice);
    }
}
