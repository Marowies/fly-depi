using MediatR;
using SkyScan.Application.Common.Interfaces;
using SkyScan.Application.Interfaces;
using SkyScan.Core.Repositories_Interfaces;

namespace SkyScan.Application.Flights.GetNearestCity
{
    public class GetNearestCityQueryHandler : IRequestHandler<GetNearestCityQuery, NearestCityResult>
    {
        private readonly ILocationLookupService _locationLookupService;
        private readonly IAirportRepository _airportRepository;
        private readonly ICurrentLanguageProvider _languageProvider;

        public GetNearestCityQueryHandler(ILocationLookupService locationLookupService, IAirportRepository airportRepository, ICurrentLanguageProvider languageProvider)
        {
            _locationLookupService = locationLookupService;
            _airportRepository = airportRepository;
            _languageProvider = languageProvider;
        }

        public async Task<NearestCityResult> Handle(GetNearestCityQuery request, CancellationToken cancellationToken)
        {
            var city = await _locationLookupService.GetNearestCityAsync(request.Lat, request.Lon);
            if (city == null)
            {
                return new NearestCityResult { Found = false };
            }

            var dbCity = await _airportRepository.GetCityByIdAsync(city.CityId);
            if (dbCity == null)
            {
                return new NearestCityResult { Found = true, CityId = city.CityId, DisplayName = city.Name };
            }

            var isAr = _languageProvider.CurrentLanguage == "ar";
            var cityName = isAr && !string.IsNullOrEmpty(dbCity.NameAr) ? dbCity.NameAr : dbCity.Name;
            var countryName = isAr && !string.IsNullOrEmpty(dbCity.Country?.NameAr) ? dbCity.Country.NameAr : dbCity.Country?.Name ?? dbCity.CountryCode;

            return new NearestCityResult
            {
                Found = true,
                CityId = dbCity.CityId,
                DisplayName = $"{cityName}, {countryName}"
            };
        }
    }
}
