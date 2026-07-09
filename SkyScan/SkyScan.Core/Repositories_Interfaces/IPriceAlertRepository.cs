using SkyScan.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SkyScan.Core.Repositories_Interfaces
{
    public interface IPriceAlertRepository : IGenericRepository<PriceAlert>
    {
        Task<IEnumerable<PriceAlert>> GetPriceAlertsByUserIdAsync(Guid userId);
        Task<PriceAlert?> FindByUserAndTripAsync(Guid userId, Guid tripId);

        /// <summary>All alerts with Trip → Flights → Airline/Airports and User eagerly loaded, for the background price check.</summary>
        Task<IEnumerable<PriceAlert>> GetAllWithDetailsAsync();

        Task<Trip> EnsureTripExistsForFlightAsync(Guid flightId, decimal price);

        Task DeleteUserAlertsAsync(Guid userId);
    }
}
