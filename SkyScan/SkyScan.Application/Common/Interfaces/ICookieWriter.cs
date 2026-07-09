namespace SkyScan.Application.Common.Interfaces
{
    /// <summary>
    /// Abstraction over appending a long-lived response cookie. Exists so Application-layer
    /// command handlers (e.g. SetLanguageCommandHandler, SetCurrencyCommandHandler) can
    /// persist a user preference without taking a direct dependency on ASP.NET Core's
    /// HttpContext/CookieOptions — that would leak the web framework into the Application
    /// layer. Implemented in SkyScan.Presentation, where HttpContext naturally lives.
    /// </summary>
    public interface ICookieWriter
    {
        /// <param name="path">
        /// Pass null to leave the cookie's Path unset (browser scopes it to the request's
        /// directory) — matches the pre-CQRS behavior of the currency cookie. Pass "/" for
        /// a site-wide cookie — matches the pre-CQRS behavior of the language cookie.
        /// </param>
        void AppendPersistent(string key, string value, string? path = null);
    }
}
