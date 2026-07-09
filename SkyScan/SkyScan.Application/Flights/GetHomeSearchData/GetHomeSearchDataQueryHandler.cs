using MediatR;
using SkyScan.Application.Common.Interfaces;
using SkyScan.Application.Flights.Common;
using SkyScan.Core.Repositories_Interfaces;

namespace SkyScan.Application.Flights.GetHomeSearchData
{
    public class GetHomeSearchDataQueryHandler : IRequestHandler<GetHomeSearchDataQuery, HomeSearchDataResult>
    {
        private readonly AirportDropdownCache _dropdownCache;
        private readonly ISearchRepository _searchRepository;
        private readonly IAirportRepository _airportRepository;
        private readonly ICurrentLanguageProvider _languageProvider;

        public GetHomeSearchDataQueryHandler(
            AirportDropdownCache dropdownCache,
            ISearchRepository searchRepository,
            IAirportRepository airportRepository,
            ICurrentLanguageProvider languageProvider)
        {
            _dropdownCache = dropdownCache;
            _searchRepository = searchRepository;
            _airportRepository = airportRepository;
            _languageProvider = languageProvider;
        }

        public async Task<HomeSearchDataResult> Handle(GetHomeSearchDataQuery request, CancellationToken cancellationToken)
        {
            var citiesDropdown = await _dropdownCache.GetAsync();
            var isAr = _languageProvider.CurrentLanguage == "ar";

            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var trending = await _searchRepository.GetTrendingRoutesSinceAsync(thirtyDaysAgo, 5);

            // GetTrendingRoutesSinceAsync already returns one Search per distinct route (top 5 by count)
            var trendingRoutesList = trending
                .Where(s => s.OriginCity != null && s.DestinationCity != null)
                .Select(s => new TrendingRouteResult
                {
                    OriginCityId = s.OriginCityId,
                    DestinationCityId = s.DestinationCityId,
                    OriginCityName = isAr && !string.IsNullOrEmpty(s.OriginCity.NameAr) ? s.OriginCity.NameAr : s.OriginCity.Name,
                    DestinationCityName = isAr && !string.IsNullOrEmpty(s.DestinationCity.NameAr) ? s.DestinationCity.NameAr : s.DestinationCity.Name,
                    SearchCount = s.OriginCity.SearchCount + s.DestinationCity.SearchCount
                })
                .ToList();

            // If we don't have 5 routes from searches, fallback to pairing database cities to guarantee exactly 5 routes
            if (trendingRoutesList.Count < 5)
            {
                var dbCities = (await _airportRepository.GetTopCitiesBySearchCountAsync(20)).ToList();
                if (dbCities.Count >= 2)
                {
                    for (int i = 0; i < dbCities.Count - 1 && trendingRoutesList.Count < 5; i++)
                    {
                        var origin = dbCities[i];
                        var dest = dbCities[i + 1];
                        if (origin.CityId != dest.CityId && !trendingRoutesList.Any(r => r.OriginCityId == origin.CityId && r.DestinationCityId == dest.CityId))
                        {
                            trendingRoutesList.Add(new TrendingRouteResult
                            {
                                OriginCityId = origin.CityId,
                                DestinationCityId = dest.CityId,
                                OriginCityName = isAr && !string.IsNullOrEmpty(origin.NameAr) ? origin.NameAr : origin.Name,
                                DestinationCityName = isAr && !string.IsNullOrEmpty(dest.NameAr) ? dest.NameAr : dest.Name,
                                SearchCount = origin.SearchCount + dest.SearchCount
                            });
                        }
                    }
                }
            }

            return new HomeSearchDataResult
            {
                CitiesDropdown = citiesDropdown,
                TrendingRoutes = trendingRoutesList.Take(5).ToList()
            };
        }
    }
}
