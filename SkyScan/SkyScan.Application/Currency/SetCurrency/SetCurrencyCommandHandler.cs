using MediatR;
using SkyScan.Application.Common.Interfaces;

namespace SkyScan.Application.Currency.SetCurrency
{
    public class SetCurrencyCommandHandler : IRequestHandler<SetCurrencyCommand>
    {
        private readonly ICookieWriter _cookieWriter;

        public SetCurrencyCommandHandler(ICookieWriter cookieWriter)
        {
            _cookieWriter = cookieWriter;
        }

        public Task Handle(SetCurrencyCommand request, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(request.Code))
            {
                // Path intentionally left unset here — the pre-CQRS controller action never
                // set CookieOptions.Path for this cookie, unlike the language cookie.
                _cookieWriter.AppendPersistent("SelectedCurrency", request.Code.ToUpper());
            }

            return Task.CompletedTask;
        }
    }
}
