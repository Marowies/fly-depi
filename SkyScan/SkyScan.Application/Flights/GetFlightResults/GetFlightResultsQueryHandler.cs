using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SkyScan.Application.Common.Interfaces;
using SkyScan.Application.DTOs;
using SkyScan.Application.Interfaces;
using SkyScan.Core.Constants;
using SkyScan.Core.Repositories_Interfaces;

namespace SkyScan.Application.Flights.GetFlightResults
{
    public class GetFlightResultsQueryHandler : IRequestHandler<GetFlightResultsQuery, FlightResultsResult>
    {
        private readonly ISearchRepository _searchRepository;
        private readonly IAirportRepository _airportRepository;
        private readonly IFlightProviderService _flightProviderService;
        private readonly IMemoryCache _cache;
        private readonly ICurrentLanguageProvider _languageProvider;
        private readonly ILogger<GetFlightResultsQueryHandler> _logger;
        private static readonly TimeSpan SearchCacheDuration = TimeSpan.FromMinutes(15);

        public GetFlightResultsQueryHandler(
            ISearchRepository searchRepository,
            IAirportRepository airportRepository,
            IFlightProviderService flightProviderService,
            IMemoryCache cache,
            ICurrentLanguageProvider languageProvider,
            ILogger<GetFlightResultsQueryHandler> logger)
        {
            _searchRepository = searchRepository;
            _airportRepository = airportRepository;
            _flightProviderService = flightProviderService;
            _cache = cache;
            _languageProvider = languageProvider;
            _logger = logger;
        }

        public async Task<FlightResultsResult> Handle(GetFlightResultsQuery request, CancellationToken cancellationToken)
        {
            // Log Search to database for trending/popular analytics
            if (request.CurrentUserId.HasValue)
            {
                try
                {
                    await _searchRepository.LogSearchAsync(request.CurrentUserId.Value, request.OriginCityId, request.DestinationCityId, request.DepartureDate, request.TripType);
                    await _airportRepository.IncrementCitySearchCountAsync(request.DestinationCityId);
                }
                catch (Exception ex)
                {
                    // Was Console.WriteLine pre-Phase-2c — same ILogger cleanup Phase 1 applied to
                    // the Infrastructure services, extended here since this handler is new code.
                    _logger.LogError(ex, "Error logging search for destination city {DestinationCityId}", request.DestinationCityId);
                }
            }

            // Resolve City Names and all Airports for the search (City-to-City support)
            var originAirports = await _airportRepository.GetAirportsByCityIdAsync(request.OriginCityId);
            var destAirports = await _airportRepository.GetAirportsByCityIdAsync(request.DestinationCityId);

            var originIatas = originAirports.Select(a => a.IataCode).Where(i => !string.IsNullOrEmpty(i)).ToList();
            var destIatas = destAirports.Select(a => a.IataCode).Where(i => !string.IsNullOrEmpty(i)).ToList();

            var isAr = _languageProvider.CurrentLanguage == "ar";

            var originCity = originAirports.FirstOrDefault()?.City;
            var originCityName = originCity != null ? (isAr && !string.IsNullOrEmpty(originCity.NameAr) ? originCity.NameAr : originCity.Name) : "Origin";
            var originCountryName = originCity?.Country != null ? (isAr && !string.IsNullOrEmpty(originCity.Country.NameAr) ? originCity.Country.NameAr : originCity.Country.Name) : originCity?.CountryCode;
            var originName = originCity != null ? $"{originCityName}, {originCountryName ?? originCity.CountryCode}" : "Origin";

            var destCity = destAirports.FirstOrDefault()?.City;
            var destCityName = destCity != null ? (isAr && !string.IsNullOrEmpty(destCity.NameAr) ? destCity.NameAr : destCity.Name) : "Destination";
            var destCountryName = destCity?.Country != null ? (isAr && !string.IsNullOrEmpty(destCity.Country.NameAr) ? destCity.Country.NameAr : destCity.Country.Name) : destCity?.CountryCode;
            var destName = destCity != null ? $"{destCityName}, {destCountryName ?? destCity.CountryCode}" : "Destination";

            var isRoundTrip = request.TripType == TripType.RoundTrip && request.ReturnDate.HasValue;

            var result = new FlightResultsResult
            {
                OriginIata = string.Join("/", originIatas),
                DestinationIata = string.Join("/", destIatas),
                OriginCity = originName,
                DestinationCity = destName,
                DepartureDate = request.DepartureDate,
                IsRoundTrip = isRoundTrip
            };

            if (isRoundTrip)
            {
                result.ReturnDate = request.ReturnDate;
                result.Flights = await SearchLegAsync(originIatas!, destIatas!, request.DepartureDate, request.ReturnDate);
            }
            else
            {
                result.Flights = await SearchLegAsync(originIatas!, destIatas!, request.DepartureDate);
            }

            return result;
        }

        /// <summary>
        /// Searches one direction of a journey (a set of origin airports to a set of destination
        /// airports on a given date), transparently caching the provider response for 15 minutes.
        /// </summary>
        private async Task<List<FlightDto>> SearchLegAsync(IEnumerable<string> originIatas, IEnumerable<string> destIatas, DateTime date, DateTime? returnDate = null)
        {
            var cacheKey = $"flights_{string.Join("-", originIatas)}_{string.Join("-", destIatas)}_{date:yyyyMMdd}" + (returnDate.HasValue ? $"_{returnDate.Value:yyyyMMdd}" : "");
            if (_cache.TryGetValue(cacheKey, out List<FlightDto>? cached) && cached != null)
            {
                return cached;
            }

            var freshFlights = await _flightProviderService.SearchFlightsAsync(originIatas, destIatas, date, returnDate);
            var flights = freshFlights.ToList();
            _cache.Set(cacheKey, flights, SearchCacheDuration);
            return flights;
        }
    }
}
