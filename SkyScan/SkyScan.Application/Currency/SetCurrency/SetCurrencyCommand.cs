using MediatR;

namespace SkyScan.Application.Currency.SetCurrency
{
    /// <summary>Persists the user's chosen display currency code as a one-year cookie.</summary>
    public record SetCurrencyCommand(string Code) : IRequest;
}
