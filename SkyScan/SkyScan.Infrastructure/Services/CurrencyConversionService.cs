using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using SkyScan.Application.Interfaces;

namespace SkyScan.Infrastructure.Services
{
    public class CurrencyConversionService : ICurrencyConversionService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private const string CacheKey = "ExchangeRates_USD";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

        public CurrencyConversionService(HttpClient httpClient, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _cache = cache;
        }

        public async Task<decimal> ConvertAsync(decimal amountUsd, string targetCurrency)
        {
            if (string.IsNullOrEmpty(targetCurrency) || targetCurrency.Equals("USD", StringComparison.OrdinalIgnoreCase))
            {
                return amountUsd;
            }

            var rates = await GetExchangeRatesAsync();
            if (rates != null && rates.TryGetValue(targetCurrency.ToUpper(), out double rate))
            {
                return amountUsd * (decimal)rate;
            }

            return amountUsd; // Fallback to USD if rate not found
        }

        public Task<string> GetCurrencySymbolAsync(string currencyCode)
        {
            string symbol = currencyCode.ToUpper() switch
            {
                "USD" => "$",
                "EGP" => "E£",
                "EUR" => "€",
                "GBP" => "£",
                _ => currencyCode
            };
            return Task.FromResult(symbol);
        }

        private async Task<Dictionary<string, double>?> GetExchangeRatesAsync()
        {
            if (!_cache.TryGetValue(CacheKey, out Dictionary<string, double>? rates) || rates == null)
            {
                try
                {
                    var response = await _httpClient.GetAsync("https://open.er-api.com/v6/latest/USD");
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("rates", out var ratesElement))
                        {
                            rates = JsonSerializer.Deserialize<Dictionary<string, double>>(ratesElement.GetRawText());
                            if (rates != null)
                            {
                                _cache.Set(CacheKey, rates, CacheDuration);
                            }
                        }
                    }
                }
                catch
                {
                    // Fallback to standard rates if API is down
                    rates = new Dictionary<string, double>
                    {
                        { "USD", 1.0 },
                        { "EGP", 48.5 },
                        { "EUR", 0.92 },
                        { "GBP", 0.79 }
                    };
                }
            }

            return rates;
        }
    }
}
