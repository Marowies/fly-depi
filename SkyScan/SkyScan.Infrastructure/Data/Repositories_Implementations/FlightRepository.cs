using SkyScan.Core.Entities.AirLine;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Infrastructure.Data.Data_Sources;

namespace SkyScan.Infrastructure.Data.Repositories_Implementations
{
    public class FlightRepository : GenericRepository<Flight>, IFlightRepository
    {
        public FlightRepository(SkyScanDbContext context) : base(context)
        {
        }

        // Specific methods for Flight can be added here
    }
}
