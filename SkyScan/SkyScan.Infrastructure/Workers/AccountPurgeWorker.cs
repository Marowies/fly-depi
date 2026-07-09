using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Infrastructure.Identity;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SkyScan.Infrastructure.Workers
{
    public class AccountPurgeWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AccountPurgeWorker> _logger;
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);
        private const int GracePeriodDays = 30;

        public AccountPurgeWorker(IServiceScopeFactory scopeFactory, ILogger<AccountPurgeWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await PurgeAccountsAsync(stoppingToken);
                await Task.Delay(CheckInterval, stoppingToken);
            }
        }

        private async Task PurgeAccountsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();

            try
            {
                // Note: userManager.Users is available if using EF Core directly, 
                // but UserManager itself doesn't expose an IQueryable by default without querying.
                // Assuming EF Core Identity stores Users as an IQueryable:
                var usersToPurge = userManager.Users
                    .Where(u => u.IsDeleted && u.DeletedAtUtc != null && u.DeletedAtUtc.Value <= DateTime.UtcNow.AddDays(-GracePeriodDays))
                    .ToList();

                foreach (var user in usersToPurge)
                {
                    if (ct.IsCancellationRequested) break;

                    var bookings = await bookingRepo.GetBookingsByUserIdAsync(user.Id);
                    foreach (var booking in bookings)
                    {
                        await bookingRepo.DeleteAsync(booking);
                    }

                    var result = await userManager.DeleteAsync(user);
                    if (!result.Succeeded)
                    {
                        _logger.LogWarning("Failed to hard-delete user {UserId}", user.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Account purge check failed");
            }
        }
    }
}
