using Microsoft.EntityFrameworkCore;
using SkyScan.Infrastructure.Data.Data_Sources;
using SkyScan.Application.Interfaces;
using SkyScan.Infrastructure.Services;
using SkyScan.Application.Services;
using SkyScan.Presentation.Middlewares;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Core.Services.Interfaces;
using SkyScan.Infrastructure.Data.Repositories_Implementations;
using Microsoft.AspNetCore.Identity;
using SkyScan.Core.Entities;
using SkyScan.Infrastructure.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using MediatR;
using FluentValidation;
using SkyScan.Application;
using SkyScan.Application.Common.Behaviors;
using SkyScan.Application.Common.Interfaces;

namespace SkyScan.Presentation
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ── Core MVC ─────────────────────────────────────────────────────────
            builder.Services.AddControllersWithViews();
            builder.Services.AddMemoryCache();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<SkyScan.Presentation.Services.ILanguageService, SkyScan.Presentation.Services.LanguageService>();
            builder.Services.AddScoped<ICookieWriter, SkyScan.Presentation.Services.CookieWriter>();
            builder.Services.AddScoped<ICurrentLanguageProvider, SkyScan.Presentation.Services.CurrentLanguageProvider>();
            builder.Services.AddScoped<IUrlBuilder, SkyScan.Presentation.Services.UrlBuilder>();
            builder.Services.AddScoped<SkyScan.Application.Flights.Common.AirportDropdownCache>();

            // ── CQRS (MediatR) + Validation Pipeline ─────────────────────────────
            // Scans SkyScan.Application for IRequestHandler<> implementations and,
            // separately, for FluentValidation IValidator<> implementations.
            builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(IApplicationAssemblyMarker).Assembly));
            builder.Services.AddValidatorsFromAssembly(typeof(IApplicationAssemblyMarker).Assembly);
            builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            // ── Database ──────────────────────────────────────────────────────────
            builder.Services.AddDbContext<SkyScanDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("SmarterASPNetConnection")));

            // ── Identity ──────────────────────────────────────────────────────────
            builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                // Password policy
                options.Password.RequireDigit           = true;
                options.Password.RequireLowercase       = true;
                options.Password.RequireUppercase       = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength         = 6;

                // Email confirmation required to sign in
                options.SignIn.RequireConfirmedEmail = true;

                // Lockout
                options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts  = 5;
                options.Lockout.AllowedForNewUsers        = true;

                // Tokens
                options.Tokens.AuthenticatorTokenProvider = TokenOptions.DefaultAuthenticatorProvider;
            })
            .AddEntityFrameworkStores<SkyScanDbContext>()
            .AddDefaultTokenProviders();

            // ── Cookie / Session settings ─────────────────────────────────────────
            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath        = "/Account/Login";
                options.LogoutPath       = "/Account/Logout";
                options.AccessDeniedPath = "/Account/AccessDenied";

                // Sliding expiration — cookie refreshed on each request within the window
                options.SlidingExpiration = true;
                options.ExpireTimeSpan    = TimeSpan.FromDays(14);
            });

            // ── Google External Authentication ─────────────────────────────────────
            var googleClientId     = builder.Configuration["Authentication:Google:ClientId"];
            var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

            if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
            {
                builder.Services.AddAuthentication()
                    .AddGoogle(options =>
                    {
                        options.ClientId     = googleClientId;
                        options.ClientSecret = googleClientSecret;
                    });
            }

            // ── Email Service ─────────────────────────────────────────────────────
            builder.Services.AddScoped<IEmailService, SmtpEmailService>();

            // ── UrlEncoder (for 2FA QR URI) ────────────────────────────────────────
            builder.Services.AddSingleton(System.Text.Encodings.Web.UrlEncoder.Default);

            // ── Repositories ──────────────────────────────────────────────────────
            builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            builder.Services.AddScoped<IFlightRepository, FlightRepository>();
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IPriceAlertRepository, PriceAlertRepository>();
            builder.Services.AddScoped<ISearchRepository, SearchRepository>();
            builder.Services.AddScoped<IAirportRepository, AirportRepository>();
            builder.Services.AddScoped<IBookingRepository, BookingRepository>();

            // ── Flight Provider & Location Lookup ──────────────────────────────────
            builder.Services.AddHttpClient<IFlightProviderService, AmadeusFlightService>();
            builder.Services.AddHttpClient<ILocationLookupService, AmadeusLocationLookupService>();
            builder.Services.AddHttpClient<IGeocodingService, NominatimGeocodingService>();

            builder.Services.AddScoped<IFlightFilteringService, FlightFilteringService>();
            builder.Services.AddHttpClient<ICurrencyConversionService, CurrencyConversionService>();

            // ── Price Alert Notifications ────────────────────────────────────────
            builder.Services.AddHostedService<SkyScan.Infrastructure.Workers.PriceAlertCheckWorker>();
            builder.Services.AddHostedService<SkyScan.Infrastructure.Workers.AccountPurgeWorker>();

            // ── Rate Limiting ────────────────────────────────────────────────────
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                // 30 search requests per minute per client IP address
                options.AddPolicy("SearchPolicy", context =>
                {
                    var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
                    return RateLimitPartition.GetFixedWindowLimiter(ipAddress, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
                });

                // 10 transactional/booking actions capacity with 2 tokens replenished every 30s per client IP address
                options.AddPolicy("BookingPolicy", context =>
                {
                    var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
                    return RateLimitPartition.GetTokenBucketLimiter(ipAddress, _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 10,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(30),
                        TokensPerPeriod = 2,
                        QueueLimit = 0
                    });
                });
            });

            // ─────────────────────────────────────────────────────────────────────
            var app = builder.Build();

            // ── Pipeline ──────────────────────────────────────────────────────────
            app.UseMiddleware<GlobalExceptionMiddleware>();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseRateLimiter();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Flight}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
