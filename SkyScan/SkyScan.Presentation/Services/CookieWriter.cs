using SkyScan.Application.Common.Interfaces;

namespace SkyScan.Presentation.Services
{
    /// <summary>ASP.NET Core-backed implementation of ICookieWriter — see that interface for why this indirection exists.</summary>
    public class CookieWriter : ICookieWriter
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CookieWriter(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public void AppendPersistent(string key, string value, string? path = null)
        {
            var options = new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) };
            if (path != null)
            {
                options.Path = path;
            }

            _httpContextAccessor.HttpContext?.Response.Cookies.Append(key, value, options);
        }
    }
}
