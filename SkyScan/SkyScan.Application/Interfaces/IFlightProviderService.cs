using SkyScan.Application.DTOs;

namespace SkyScan.Application.Interfaces
{
    public interface IFlightProviderService
    {
        Task<IEnumerable<FlightDto>> SearchFlightsAsync(string originIata, string destinationIata, DateTime departureDate);
    }
}
