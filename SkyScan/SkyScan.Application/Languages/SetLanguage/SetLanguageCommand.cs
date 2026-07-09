using MediatR;

namespace SkyScan.Application.Languages.SetLanguage
{
    /// <summary>Persists the user's chosen UI language ("ar"/"en") as a site-wide, one-year cookie.</summary>
    public record SetLanguageCommand(string Culture) : IRequest;
}
