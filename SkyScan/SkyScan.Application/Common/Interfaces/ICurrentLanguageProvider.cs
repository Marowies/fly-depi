namespace SkyScan.Application.Common.Interfaces
{
    /// <summary>
    /// Abstraction over "what UI language is the current request in" ("en"/"ar"). Exists so
    /// Application-layer handlers that need to pick a localized city/country name (e.g. for the
    /// airport dropdown or trending routes) don't depend on SkyScan.Presentation.Services.ILanguageService
    /// directly — Presentation depends on Application, never the reverse. Implemented in
    /// SkyScan.Presentation by wrapping the existing ILanguageService.
    /// </summary>
    public interface ICurrentLanguageProvider
    {
        string CurrentLanguage { get; }
    }
}
