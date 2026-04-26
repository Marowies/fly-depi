using SkyScan.Application.DTOs;
using System.Collections.Generic;

namespace SkyScan.Application.Interfaces
{
    public interface IFlightFilteringService
    {
        IEnumerable<FlightDto> FilterAndSort(IEnumerable<FlightDto> flights, FlightFilterCriteriaDto criteria);
    }
}
