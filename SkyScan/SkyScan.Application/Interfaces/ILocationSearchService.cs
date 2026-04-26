using SkyScan.Application.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SkyScan.Application.Interfaces
{
    public interface ILocationSearchService
    {
        IEnumerable<CitySuggestionDto> Search(string query, int limit = 10);
        Task InitializeAsync();
    }
}
