using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SkyScan.Application.DTOs;
using SkyScan.Application.Interfaces;

namespace SkyScan.Infrastructure.Services
{
    public class AviationStackFlightService : IFlightProviderService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public AviationStackFlightService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration.GetValue<string>("FlightProviderSettings:AviationStackApiKey") ?? "";
            
            var baseUrl = configuration.GetValue<string>("AviationStack:BaseUrl") ?? "http://api.aviationstack.com/v1/";
            _httpClient.BaseAddress = new Uri(baseUrl);
        }

        public async Task<IEnumerable<FlightDto>> SearchFlightsAsync(string originIata, string destinationIata, DateTime departureDate)
        {
            var flights = new List<FlightDto>();
            
            // Using the /timetable endpoint for scheduled flights as requested
            var url = $"timetable?access_key={_apiKey}&iataCode={originIata}&type=departure";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    using var document = JsonDocument.Parse(jsonContent);
                    var root = document.RootElement;

                    if (root.TryGetProperty("data", out var dataArray))
                    {
                        foreach (var item in dataArray.EnumerateArray())
                        {
                            var departure = item.GetProperty("departure");
                            var arrival = item.GetProperty("arrival");
                            var flight = item.GetProperty("flight");
                            var airline = item.GetProperty("airline");

                            // The timetable endpoint returns all departures for the origin. 
                            // We must manually filter to ensure the arrival airport matches the destination requested.
                            var arrIata = arrival.GetProperty("iataCode").GetString();
                            if (!string.IsNullOrEmpty(destinationIata) && !string.Equals(arrIata, destinationIata, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            var scheduledTimeStr = departure.GetProperty("scheduledTime").GetString();
                            if (DateTime.TryParse(scheduledTimeStr, out var flightDate))
                            {
                                flights.Add(new FlightDto
                                {
                                    AirlineName = airline.GetProperty("name").GetString() ?? "Unknown",
                                    FlightNumber = flight.GetProperty("iata").GetString() ?? "",
                                    OriginAirport = departure.GetProperty("iata").GetString() ?? "",
                                    DestinationAirport = arrival.GetProperty("iata").GetString() ?? "",
                                    DepartureTime = flightDate,
                                    ArrivalTime = DateTime.TryParse(arrival.GetProperty("scheduledTime").GetString(), out var arrTime) ? arrTime : flightDate.AddHours(2),
                                    Status = item.GetProperty("flight_status").GetString() ?? "",
                                    Price = new Random().Next(100, 1500) // Missing in standard response
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Optionally log exception here
            }

            return flights;
        }
    }
}
