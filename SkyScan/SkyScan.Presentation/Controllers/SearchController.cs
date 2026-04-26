using Microsoft.AspNetCore.Mvc;
using SkyScan.Application.Interfaces;
using System.Threading.Tasks;

namespace SkyScan.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SearchController : ControllerBase
    {
        private readonly ILocationSearchService _searchService;

        public SearchController(ILocationSearchService searchService)
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
