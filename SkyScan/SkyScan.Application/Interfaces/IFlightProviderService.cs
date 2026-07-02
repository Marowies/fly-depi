using SkyScan.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SkyScan.Application.Interfaces
{
    public interface IFlightProviderService
    {
        Task<IEnumerable<FlightDto>> SearchFlightsAsync(
            IEnumerable<string> originIatas, 
            IEnumerable<string> destinationIatas, 
            DateTime departureDate, 
            DateTime? returnDate = null);

        Task<string?> GetAirlineNameAsync(string iataCode);
    }
}
