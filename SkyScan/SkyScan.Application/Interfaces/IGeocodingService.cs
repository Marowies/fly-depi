using System.Threading.Tasks;

namespace SkyScan.Application.Interfaces
{
    public interface IGeocodingService
    {
        Task<string?> ReverseGeocodeCityNameAsync(double latitude, double longitude);
    }
}
