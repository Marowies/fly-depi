using Microsoft.AspNetCore.Mvc;
using SkyScan.Application.Interfaces;
using System.Threading.Tasks;

namespace SkyScan.Presentation.Controllers
{
    /// <summary>
    /// City-name autocomplete for the search form's typeahead, backed by an in-memory
    /// index (ILocationSearchService). Renamed from SearchController — it has no
    /// relationship to ISearchRepository (trending/recent searches), which lives in
    /// FlightController; see Phase 2b Flag F1 and Phase 2c report Flag F1.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class CityAutocompleteController : ControllerBase
    {
        private readonly ILocationSearchService _searchService;

        public CityAutocompleteController(ILocationSearchService searchService)
        {
            _searchService = searchService;
        }

        [HttpGet("cities")]
        public IActionResult SearchCities(string q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return Ok(new object[] { });

            var results = _searchService.Search(q);
            return Ok(results);
        }
    }
}
