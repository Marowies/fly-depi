using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkyScan.Application.DTOs;
using SkyScan.Application.Interfaces;
using SkyScan.Core.Entities;
using SkyScan.Core.Entities.AirLine;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Core.Services.Interfaces;
using SkyScan.Infrastructure.Identity;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SkyScan.Infrastructure.Workers
{
    /// <summary>
    /// Periodically re-checks active PriceAlerts against fresh provider prices and emails the
    /// user when their target is hit. Route/date come from the alert's linked Trip → Flight →
    /// Airport, so no separate schema is needed to track what's being watched. Runs off the
    /// request thread — an In-Site/SignalR notification can be dispatched alongside the email
    /// below without changing how alerts are evaluated. One-shot: the alert is deleted once it
    /// fires, so no extra "already notified" column is required.
    /// </summary>
    public class PriceAlertCheckWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PriceAlertCheckWorker> _logger;
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

        public PriceAlertCheckWorker(IServiceScopeFactory scopeFactory, ILogger<PriceAlertCheckWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await CheckAlertsAsync(stoppingToken);
                await Task.Delay(CheckInterval, stoppingToken);
            }
        }

        private async Task CheckAlertsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var alertRepo = scope.ServiceProvider.GetRequiredService<IPriceAlertRepository>();
            var provider = scope.ServiceProvider.GetRequiredService<IFlightProviderService>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var alerts = (await alertRepo.GetAllWithDetailsAsync())
                .Where(a => a.Trip?.Flights?.FirstOrDefault()?.DepartureTime > DateTime.UtcNow)
                .ToList();

            foreach (var alert in alerts)
            {
                if (ct.IsCancellationRequested) break;
                await EvaluateAlertAsync(alert, provider, alertRepo, emailService, userManager);
            }
        }

        private async Task EvaluateAlertAsync(
            PriceAlert alert, IFlightProviderService provider, IPriceAlertRepository alertRepo,
            IEmailService emailService, UserManager<ApplicationUser> userManager)
        {
            var flight = alert.Trip?.Flights?.FirstOrDefault();
            var originIata = flight?.DepartureAirport?.IataCode;
            var destinationIata = flight?.ArrivalAirport?.IataCode;
            if (flight == null || string.IsNullOrEmpty(originIata) || string.IsNullOrEmpty(destinationIata)) return;

            try
            {
                var results = await provider.SearchFlightsAsync(
                    new[] { originIata }, new[] { destinationIata }, flight.DepartureTime.Date);

                var match = results.FirstOrDefault(f => f.FlightNumber == flight.FlightNumber);
                
                bool favoritedFlightCheaper = match != null && match.Price < alert.TargetPrice;
                var cheaperAlternatives = results
                    .Where(f => f.FlightNumber != flight.FlightNumber && f.Price < alert.TargetPrice)
                    .OrderBy(f => f.Price)
                    .ToList();

                if (!favoritedFlightCheaper && !cheaperAlternatives.Any()) return;

                var user = await userManager.FindByIdAsync(alert.UserId.ToString());
                if (user?.Email == null) return;

                string emailBody = BuildAlertEmailBody(flight, alert.TargetPrice, match, cheaperAlternatives);
                await emailService.SendEmailAsync(user.Email, "SkyScan Price Drop & Flight Alert", emailBody);
                var trackedAlert = (await alertRepo.FindAsync(a => a.Id == alert.Id)).FirstOrDefault();
                if (trackedAlert != null)
                {
                    await alertRepo.DeleteAsync(trackedAlert);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Price check failed for alert {AlertId}", alert.Id);
            }
        }

        private static string BuildAlertEmailBody(
            Flight flight, 
            decimal originalPrice, 
            FlightDto? currentMatch, 
            System.Collections.Generic.List<FlightDto> cheaperAlternatives)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("<div style=\"font-family:Manrope,Inter,sans-serif;background:#050505;color:#F8F8F8;padding:40px;border-radius:16px;max-width:600px;margin:auto;border:1px solid rgba(255,255,255,0.08);\">");
            sb.Append("<h2 style=\"color:#D8B46A;font-family:'Playfair Display',serif;margin-bottom:16px;\">SkyScan Flight Alert</h2>");
            
            if (currentMatch != null && currentMatch.Price < originalPrice)
            {
                sb.Append($"<p style=\"color:#B9B9B9;line-height:1.6;font-size:14px;\">Good news! Your watched flight <strong>{flight.Airline?.Name} {flight.FlightNumber}</strong> on {flight.DepartureTime:dd MMM yyyy} has dropped from <strong>${originalPrice:N0}</strong> to <strong style=\"color:#D8B46A;\">${currentMatch.Price:N0}</strong>!</p>");
            }
            else
            {
                sb.Append($"<p style=\"color:#B9B9B9;line-height:1.6;font-size:14px;\">We are monitoring your route from <strong>{flight.DepartureAirport?.City?.Name} ({flight.DepartureAirport?.IataCode})</strong> to <strong>{flight.ArrivalAirport?.City?.Name} ({flight.ArrivalAirport?.IataCode})</strong> on {flight.DepartureTime:dd MMM yyyy}.</p>");
            }

            if (cheaperAlternatives.Any())
            {
                sb.Append("<h3 style=\"color:#D8B46A;font-size:16px;margin-top:24px;border-top:1px solid rgba(255,255,255,0.08);padding-top:16px;\">Cheaper Alternatives Found:</h3>");
                sb.Append("<table style=\"width:100%;border-collapse:collapse;margin-top:12px;font-size:13px;text-align:left;\">");
                sb.Append("<thead><tr style=\"border-bottom:1px solid rgba(255,255,255,0.08);color:#B9B9B9;\">");
                sb.Append("<th style=\"padding:8px;\">Airline</th><th style=\"padding:8px;\">Flight</th><th style=\"padding:8px;\">Departure</th><th style=\"padding:8px;text-align:right;\">Price</th></tr></thead>");
                sb.Append("<tbody>");
                
                foreach (var alt in cheaperAlternatives.GetRange(0, Math.Min(3, cheaperAlternatives.Count)))
                {
                    sb.Append("<tr style=\"border-bottom:1px solid rgba(255,255,255,0.05);\">");
                    sb.Append($"<td style=\"padding:8px;\">{alt.AirlineName}</td>");
                    sb.Append($"<td style=\"padding:8px;\">{alt.FlightNumber}</td>");
                    sb.Append($"<td style=\"padding:8px;\">{alt.DepartureTime:HH:mm}</td>");
                    sb.Append($"<td style=\"padding:8px;color:#D8B46A;font-weight:bold;text-align:right;\">${alt.Price:N0}</td>");
                    sb.Append("</tr>");
                }
                sb.Append("</tbody></table>");
            }
            
            sb.Append("<p style=\"color:#B9B9B9;font-size:11px;margin-top:32px;text-align:center;opacity:0.6;\">This is an automated alert from SkyScan luxury travel.</p>");
            sb.Append("</div>");
            return sb.ToString();
        }
    }
}
