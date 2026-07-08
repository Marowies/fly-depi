using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SkyScan.Application.DTOs;
using SkyScan.Application.Interfaces;
using SkyScan.Core.Repositories_Interfaces;

namespace SkyScan.Infrastructure.Services
{
    public class AmadeusLocationLookupService : ILocationLookupService
    {
        private readonly HttpClient _httpClient;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly IAirportRepository _airportRepository;
        private readonly IGeocodingService _geocodingService;
        private string? _accessToken;
        private DateTime _tokenExpiration = DateTime.MinValue;

        public AmadeusLocationLookupService(HttpClient httpClient, IConfiguration configuration, IAirportRepository airportRepository, IGeocodingService geocodingService)
        {
            _httpClient = httpClient;
            _clientId = configuration["Amadeus:ClientId"] ?? "";
            _clientSecret = configuration["Amadeus:ClientSecret"] ?? "";
            _airportRepository = airportRepository;
            _geocodingService = geocodingService;

            var baseUrl = configuration["Amadeus:BaseUrl"] ?? "https://test.api.amadeus.com/";
            _httpClient.BaseAddress = new Uri(baseUrl);
        }

        private async Task EnsureAccessTokenAsync()
        {
            if (_accessToken != null && DateTime.UtcNow < _tokenExpiration)
            {
                return;
            }

            var requestContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("client_secret", _clientSecret)
            });

            var response = await _httpClient.PostAsync("v1/security/oauth2/token", requestContent);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed to retrieve Amadeus access token.");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            _accessToken = root.GetProperty("access_token").GetString();
            var expiresIn = root.GetProperty("expires_in").GetInt32();
            _tokenExpiration = DateTime.UtcNow.AddSeconds(expiresIn - 10);
        }

        public async Task<NearestCityDto?> GetNearestCityAsync(double latitude, double longitude)
        {
            try
            {
                await EnsureAccessTokenAsync();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                // Default radius to 500km to maximize finding a matching city in seeded database
                var url = $"v1/reference-data/locations/airports?latitude={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&longitude={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&radius=500";
                
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var location in dataArray.EnumerateArray())
                        {
                            var iataCode = location.TryGetProperty("iataCode", out var iataEl) ? iataEl.GetString() : null;
                            if (string.IsNullOrEmpty(iataCode)) continue;

                            // Resolve the IATA code against local seeded data
                            var airport = await _airportRepository.GetByIataAsync(iataCode);
                            if (airport != null && airport.City != null)
                            {
                                return new NearestCityDto
                                {
                                    CityId = airport.City.CityId,
                                    Name = airport.City.Name
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error finding nearest airport/city via Amadeus: {ex.Message}");
            }

            // Fallback: Use geocoding service + local database lookup
            try
            {
                var cityName = await _geocodingService.ReverseGeocodeCityNameAsync(latitude, longitude);
                if (!string.IsNullOrEmpty(cityName))
                {
                    var city = await _airportRepository.GetCityByNameAsync(cityName);
                    if (city != null)
                    {
                        return new NearestCityDto
                        {
                            CityId = city.CityId,
                            Name = city.Name
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in coordinate reverse-geocoding fallback: {ex.Message}");
            }

            return null;
        }
    }
}
