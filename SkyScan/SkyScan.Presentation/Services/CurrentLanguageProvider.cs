using SkyScan.Application.Common.Interfaces;

namespace SkyScan.Presentation.Services
{
    /// <summary>Wraps the existing ILanguageService so Application-layer handlers can read the current UI language without depending on Presentation. See ICurrentLanguageProvider for why this indirection exists.</summary>
    public class CurrentLanguageProvider : ICurrentLanguageProvider
    {
        private readonly ILanguageService _languageService;

        public CurrentLanguageProvider(ILanguageService languageService)
        {
            _languageService = languageService;
        }

        public string CurrentLanguage => _languageService.CurrentLanguage;
    }
}
