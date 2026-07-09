using Microsoft.AspNetCore.Routing;
using SkyScan.Application.Common.Interfaces;

namespace SkyScan.Presentation.Services
{
    public class UrlBuilder : IUrlBuilder
    {
        private readonly LinkGenerator _linkGenerator;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UrlBuilder(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
        {
            _linkGenerator = linkGenerator;
            _httpContextAccessor = httpContextAccessor;
        }

        public string ActionUrl(string action, string controller, object values) =>
            _linkGenerator.GetUriByAction(_httpContextAccessor.HttpContext!, action, controller, values)
                ?? throw new InvalidOperationException($"Could not generate URL for {controller}/{action}.");
    }
}
