using MediatR;
using SkyScan.Application.Common.Interfaces;

namespace SkyScan.Application.Languages.SetLanguage
{
    public class SetLanguageCommandHandler : IRequestHandler<SetLanguageCommand>
    {
        private readonly ICookieWriter _cookieWriter;

        public SetLanguageCommandHandler(ICookieWriter cookieWriter)
        {
            _cookieWriter = cookieWriter;
        }

        public Task Handle(SetLanguageCommand request, CancellationToken cancellationToken)
        {
            // Same whitelist the controller action enforced pre-conversion: anything outside
            // "ar"/"en" is silently ignored rather than rejected — not a validation error.
            if (request.Culture is "ar" or "en")
            {
                _cookieWriter.AppendPersistent("Language", request.Culture, path: "/");
            }

            return Task.CompletedTask;
        }
    }
}
