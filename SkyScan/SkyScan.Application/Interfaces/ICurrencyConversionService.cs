using System.Threading.Tasks;

namespace SkyScan.Application.Interfaces
{
    public interface ICurrencyConversionService
    {
        Task<decimal> ConvertAsync(decimal amountUsd, string targetCurrency);
        Task<string> GetCurrencySymbolAsync(string currencyCode);
    }
}
