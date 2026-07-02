using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration; // Added for reading API key safely
using SkyScan.Application.Interfaces;

namespace SkyScan.Infrastructure.Services
{
    public class CurrencyConversionService : ICurrencyConversionService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly string _apiKey;
        private const string CacheKey = "ExchangeRates_USD";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(1);

        // Best practice: Inject IConfiguration to avoid hardcoding your CurrencyFreaks API key
        public CurrencyConversionService(HttpClient httpClient, IMemoryCache cache, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _cache = cache;
            _apiKey = configuration["CurrencyFreaks:ApiKey"];
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

            return amountUsd; // Fallback to original USD amount if rate not found
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
                    // Target CurrencyFreaks and strictly filter for EGP, EUR, and GBP to save bandwidth
                    string url = $"https://api.currencyfreaks.com/v2.0/rates/latest?apikey={_apiKey}&symbols=EGP,EUR,GBP";
                    
                    var response = await _httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        
                        // CurrencyFreaks nests the key/value pairs inside "rates"
                        if (root.TryGetProperty("rates", out var ratesElement))
                        {
                            // CurrencyFreaks returns values as strings (e.g., "EGP": "47.95"). 
                            // We need to parse them from string to double properly.
                            var rawRates = JsonSerializer.Deserialize<Dictionary<string, string>>(ratesElement.GetRawText());
                            
                            if (rawRates != null)
                            {
                                rates = new Dictionary<string, double>();
                                foreach (var kvp in rawRates)
                                {
                                    if (double.TryParse(kvp.Value, out double parsedRate))
                                    {
                                        rates[kvp.Key.ToUpper()] = parsedRate;
                                    }
                                }

                                _cache.Set(CacheKey, rates, CacheDuration);
                            }
                        }
                    }
                }
                catch
                {
                    // Fail gracefully with your hardcoded fallback rates if the API goes down
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