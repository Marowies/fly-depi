using SkyScan.Core.Entities.AirLine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SkyScan.Core.Repositories_Interfaces
{
    public interface IFlightRepository : IGenericRepository<Flight>
    {
        Task<IEnumerable<Flight>> SearchFlightsAsync(IEnumerable<string> originIatas, IEnumerable<string> destinationIatas, DateTime departureDate);
        Task<IEnumerable<Flight>> GetLowestPriceFlightsAsync(int count = 5);
        Task<IEnumerable<Flight>> GetFlightsAroundTheWorldAsync(int count = 5);
        Task<Flight?> GetByFlightNumberAndDepartureAsync(string flightNumber, DateTime departureTime);
        Task<Flight?> EnsureFlightExistsAsync(
            string flightNumber, 
            DateTime departureTime, 
            string originIata, 
            string destinationIata, 
            string airlineName, 
            DateTime arrivalTime, 
            string redirectUrl,
            decimal price = 0.00M,
            bool hasWifi = false,
            bool hasFood = false,
            bool hasEntertainment = false);
    }
}
