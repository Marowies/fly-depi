using System;

namespace SkyScan.Application.DTOs
{
    public class CitySuggestionDto
    {
        public Guid CityId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string CountryCode { get; set; } = string.Empty;
        public int Rank { get; set; }
    }
}
