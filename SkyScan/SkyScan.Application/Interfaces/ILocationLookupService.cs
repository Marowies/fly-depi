using SkyScan.Application.DTOs;
using System.Threading.Tasks;

namespace SkyScan.Application.Interfaces
{
    public interface ILocationLookupService
    {
        Task<NearestCityDto?> GetNearestCityAsync(double latitude, double longitude);
    }
}
