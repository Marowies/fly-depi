using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkyScan.Application.DTOs;
using SkyScan.Application.Interfaces;
using SkyScan.Core.Entities;
using SkyScan.Core.Entities.AirLine;
using Microsoft.EntityFrameworkCore;
using SkyScan.Infrastructure.Data.Data_Sources;

namespace SkyScan.Infrastructure.Services
{
    public class AmadeusFlightService : IFlightProviderService
    {
        private readonly HttpClient _httpClient;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly IServiceProvider _serviceProvider;
        private string? _accessToken;
        private DateTime _tokenExpiration = DateTime.MinValue;

        public AmadeusFlightService(HttpClient httpClient, IConfiguration configuration, IServiceProvider serviceProvider)
        {
            _httpClient = httpClient;
            _clientId = configuration["Amadeus:ClientId"] ?? "";
            _clientSecret = configuration["Amadeus:ClientSecret"] ?? "";
            _serviceProvider = serviceProvider;
            
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

        public async Task<IEnumerable<FlightDto>> SearchFlightsAsync(IEnumerable<string> originIatas, IEnumerable<string> destinationIatas, DateTime departureDate)
        {
            var flightDtos = new List<FlightDto>();
            await EnsureAccessTokenAsync();

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            foreach (var origin in originIatas)
            {
                foreach (var destination in destinationIatas)
                {
                    var dateStr = departureDate.ToString("yyyy-MM-dd");
                    var url = $"v2/shopping/flight-offers?originLocationCode={origin}&destinationLocationCode={destination}&departureDate={dateStr}&adults=1&max=10";

                    try
                    {
                        var response = await _httpClient.GetAsync(url);
                        if (!response.IsSuccessStatusCode) continue;

                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        if (!doc.RootElement.TryGetProperty("data", out var dataArray)) continue;

                        using var scope = _serviceProvider.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<SkyScanDbContext>();

                        foreach (var offer in dataArray.EnumerateArray())
                        {
                            var price = decimal.Parse(offer.GetProperty("price").GetProperty("grandTotal").GetString() ?? "0.00");
                            var firstItinerary = offer.GetProperty("itineraries").EnumerateArray().First();
                            var segments = firstItinerary.GetProperty("segments").EnumerateArray().ToList();

                            if (!segments.Any()) continue;

                            var firstSegment = segments.First();
                            var lastSegment = segments.Last();

                            var carrierCode = firstSegment.GetProperty("carrierCode").GetString() ?? "XX";
                            var flightNum = firstSegment.GetProperty("number").GetString() ?? "000";
                            var departureTime = DateTime.Parse(firstSegment.GetProperty("departure").GetProperty("at").GetString() ?? DateTime.Now.ToString());
                            var arrivalTime = DateTime.Parse(lastSegment.GetProperty("arrival").GetProperty("at").GetString() ?? DateTime.Now.ToString());

                            // Find or Create Airline in local DB
                            var airline = await dbContext.Airlines.FirstOrDefaultAsync(a => a.IataCode == carrierCode);
                            if (airline == null)
                            {
                                airline = new Airline 
                                { 
                                    AirlineId = Guid.NewGuid(), 
                                    Name = carrierCode + " Airlines", 
                                    IataCode = carrierCode,
                                    HotlineNumber = "+1-800-555-0199" 
                                };
                                dbContext.Airlines.Add(airline);
                                await dbContext.SaveChangesAsync();
                            }

                            // Find or Create Airplane in local DB
                            var airplane = await dbContext.Airplanes.FirstOrDefaultAsync();
                            if (airplane == null)
                            {
                                airplane = new Airplane 
                                { 
                                    AirplaneId = Guid.NewGuid(), 
                                    Model = "Boeing 777", 
                                    ManufactureCompany = "Boeing", 
                                    OwnerCompany = carrierCode + " Airlines",
                                    ManufactureDate = new DateOnly(2020, 1, 1),
                                    PlaneId = Guid.NewGuid().ToString().Substring(0, 8),
                                    Seats = 300 
                                };
                                dbContext.Airplanes.Add(airplane);
                                await dbContext.SaveChangesAsync();
                            }

                            // Find departure and arrival airport
                            var depAirport = await dbContext.Airports.FirstOrDefaultAsync(a => a.IataCode == origin);
                            var arrAirport = await dbContext.Airports.FirstOrDefaultAsync(a => a.IataCode == destination);

                            if (depAirport == null || arrAirport == null) continue;

                            // Dynamic Google Flights redirection link
                            var redirectUrl = $"https://www.google.com/travel/flights?q=Flights%20to%20{destination}%20from%20{origin}%20on%20{dateStr}";

                            // Persist to local DB if not exists
                            var flight = await dbContext.Flights
                                .Include(f => f.Tickets)
                                .FirstOrDefaultAsync(f => f.FlightNumber == $"{carrierCode} {flightNum}" && f.DepartureTime == departureTime);

                            if (flight == null)
                            {
                                flight = new Flight
                                {
                                    FlightId = Guid.NewGuid(),
                                    AirlineId = airline.AirlineId,
                                    AirplaneId = airplane.AirplaneId,
                                    FlightNumber = $"{carrierCode} {flightNum}",
                                    DepartureAirportId = depAirport.AirportId,
                                    ArrivalAirportId = arrAirport.AirportId,
                                    DepartureTime = departureTime,
                                    ArrivalTime = arrivalTime,
                                    RedirectURL = redirectUrl
                                };

                                dbContext.Flights.Add(flight);
                                await dbContext.SaveChangesAsync();

                                // Add a Ticket
                                var ticket = new Ticket
                                {
                                    TicketId = Guid.NewGuid(),
                                    Price = price,
                                    CabinClass = Core.Constants.CabinType.Economy,
                                    FlightId = flight.FlightId,
                                    HasFood = true,
                                    HasWifi = true
                                };
                                dbContext.Tickets.Add(ticket);
                                await dbContext.SaveChangesAsync();
                            }

                            flightDtos.Add(new FlightDto
                            {
                                AirlineName = airline.Name,
                                FlightNumber = flight.FlightNumber,
                                OriginAirport = origin,
                                DestinationAirport = destination,
                                DepartureTime = departureTime,
                                ArrivalTime = arrivalTime,
                                Price = price,
                                Stops = Math.Max(0, segments.Count - 1),
                                Status = "Active",
                                RedirectURL = redirectUrl
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error querying flight from {origin} to {destination}: {ex.Message}");
                    }
                }
            }

            return flightDtos;
        }
    }
}
