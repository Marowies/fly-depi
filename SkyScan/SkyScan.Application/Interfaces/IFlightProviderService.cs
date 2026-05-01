using SkyScan.Application.DTOs;

namespace SkyScan.Application.Interfaces
{
    public interface IFlightProviderService
    {
        Task<IEnumerable<FlightDto>> SearchFlightsAsync(IEnumerable<string> originIatas, IEnumerable<string> destinationIatas, DateTime departureDate);
    }
}
