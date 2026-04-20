using System.Text.Json;
using SkyScan.Application.DTOs;
using SkyScan.Application.Interfaces;

namespace SkyScan.Infrastructure.Services
{
    public class MockFlightProviderService : IFlightProviderService
    {
        public async Task<IEnumerable<FlightDto>> SearchFlightsAsync(string originIata, string destinationIata, DateTime departureDate)
        {
            // Resolve the physical path to wwwroot/Json/FlightSchedules.json
            var jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Json", "FlightSchedules.json");
            
            if (!File.Exists(jsonPath))
            {
                return new List<FlightDto>();
            }

            var jsonContent = await File.ReadAllTextAsync(jsonPath);
            
            using var document = JsonDocument.Parse(jsonContent);
            var root = document.RootElement;

            var flights = new List<FlightDto>();

            if (root.TryGetProperty("data", out var dataArray))
            {
                foreach (var item in dataArray.EnumerateArray())
                {
                    var departure = item.GetProperty("departure");
                    var arrival = item.GetProperty("arrival");
                    var flight = item.GetProperty("flight");
                    var airline = item.GetProperty("airline");

                    var flightOrigin = departure.GetProperty("iata").GetString();
                    var flightDest = arrival.GetProperty("iata").GetString();
                    var scheduledTimeStr = departure.GetProperty("scheduledTime").GetString();

                    if (DateTime.TryParse(scheduledTimeStr, out var flightDate))
                    {
                        // Match on dates (simplified logic: just the date part)
                        // Also conditionally filter by origin/destination if they are provided
                        bool originMatch = string.IsNullOrEmpty(originIata) || string.Equals(flightOrigin, originIata, StringComparison.OrdinalIgnoreCase);
                        bool destMatch = string.IsNullOrEmpty(destinationIata) || string.Equals(flightDest, destinationIata, StringComparison.OrdinalIgnoreCase);
                        
                        if (originMatch && destMatch) // Note: removing date filter temporarily to ensure mock data returns results regardless of mock dates
                        {
                            flights.Add(new FlightDto
                            {
                                AirlineName = airline.GetProperty("name").GetString() ?? "Unknown",
                                FlightNumber = flight.GetProperty("iata").GetString() ?? "",
                                OriginAirport = flightOrigin ?? "",
                                DestinationAirport = flightDest ?? "",
                                DepartureTime = flightDate,
                                ArrivalTime = DateTime.TryParse(arrival.GetProperty("scheduledTime").GetString(), out var arrTime) ? arrTime : flightDate.AddHours(2),
                                Status = item.GetProperty("flight_status").GetString() ?? "",
                                Price = new Random().Next(100, 1500) // Mock random price for presentation
                            });
                        }
                    }
                }
            }

            return flights;
        }
    }
}
