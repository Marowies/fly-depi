using SkyScan.Core.Entities.AirLine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SkyScan.Core.Repositories_Interfaces
{
    public interface IFlightRepository : IGenericRepository<Flight>
    {
        Task<IEnumerable<Flight>> SearchFlightsAsync(Guid originCityId, Guid destinationCityId, DateTime departureDate);
        Task<IEnumerable<Flight>> GetLowestPriceFlightsAsync(int count = 5);
        Task<IEnumerable<Flight>> GetFlightsAroundTheWorldAsync(int count = 5);
    }
}
