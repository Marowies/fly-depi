using SkyScan.Core.Entities;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Infrastructure.Data.Data_Sources;

namespace SkyScan.Infrastructure.Data.Repositories_Implementations
{
    public class PriceAlertRepository : GenericRepository<PriceAlert>, IPriceAlertRepository
    {
        public PriceAlertRepository(SkyScanDbContext context) : base(context)
        {
        }
    }
}
