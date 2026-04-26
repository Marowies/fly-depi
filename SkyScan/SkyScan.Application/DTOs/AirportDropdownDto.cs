namespace SkyScan.Application.DTOs
{
    /// <summary>
    /// Lightweight projection DTO for airport dropdown lists.
    /// Only fetches the 3 columns actually needed — avoids loading full entity graphs.
    /// </summary>
    public class AirportDropdownDto
    {
        public string IataCode { get; set; } = string.Empty;
        public string AirportName { get; set; } = string.Empty;
        public string CityName { get; set; } = string.Empty;
    }
}
