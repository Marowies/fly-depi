using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using SkyScan.Application.Interfaces;

namespace SkyScan.Infrastructure.Services
{
    public class NominatimGeocodingService : IGeocodingService
    {
        private readonly HttpClient _httpClient;

        public NominatimGeocodingService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            // Nominatim requires a User-Agent identifying the app
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SkyScanApp/1.0 (contact: skyscanorg@gmail.com)");
        }

        public async Task<string?> ReverseGeocodeCityNameAsync(double latitude, double longitude)
        {
            try
            {
                // Nominatim reverse API uses 'lon' instead of 'longitude'
                var latStr = latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var lonStr = longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var url = $"https://nominatim.openstreetmap.org/reverse?format=json&lat={latStr}&lon={lonStr}&zoom=10&accept-language=en";

                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("address", out var addressEl))
                    {
                        if (addressEl.TryGetProperty("city", out var cityEl)) return cityEl.GetString();
                        if (addressEl.TryGetProperty("town", out var townEl)) return townEl.GetString();
                        if (addressEl.TryGetProperty("village", out var villageEl)) return villageEl.GetString();
                        if (addressEl.TryGetProperty("suburb", out var suburbEl)) return suburbEl.GetString();
                        if (addressEl.TryGetProperty("county", out var countyEl)) return countyEl.GetString();
                        if (addressEl.TryGetProperty("state", out var stateEl)) return stateEl.GetString();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Nominatim reverse geocoding: {ex.Message}");
            }
            return null;
        }
    }
}
