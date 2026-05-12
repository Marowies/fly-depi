using Microsoft.EntityFrameworkCore;
using SkyScan.Infrastructure.Data.Data_Sources;
using SkyScan.Application.Interfaces;
using SkyScan.Infrastructure.Services;
using SkyScan.Application.Mappings;
using SkyScan.Presentation.Middlewares;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Infrastructure.Data.Repositories_Implementations;
using Microsoft.AspNetCore.Identity;
using SkyScan.Core.Entities;

namespace SkyScan.Presentation
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ── Core MVC ─────────────────────────────────────────────────────────
            builder.Services.AddControllersWithViews();
            builder.Services.AddMemoryCache();

            // ── Database ──────────────────────────────────────────────────────────
            builder.Services.AddDbContext<SkyScanDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // ── AutoMapper ────────────────────────────────────────────────────────
            builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

            // ── Identity ──────────────────────────────────────────────────────────
            builder.Services.AddIdentity<User, IdentityRole<Guid>>(options =>
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

            // ── Flight Provider ────────────────────────────────────────────────────
            var useMockData = builder.Configuration["FlightProviderSettings:UseMockData"] == "true";
            if (useMockData)
                builder.Services.AddScoped<IFlightProviderService, MockFlightProviderService>();
            else
                builder.Services.AddHttpClient<IFlightProviderService, AviationStackFlightService>();

            builder.Services.AddSingleton<ILocationSearchService, LocationSearchService>();
            builder.Services.AddScoped<IFlightFilteringService, FlightFilteringService>();

            // ─────────────────────────────────────────────────────────────────────
            var app = builder.Build();

            // Warm up the In-Memory Search Index
            using (var scope = app.Services.CreateScope())
            {
                var searchService = scope.ServiceProvider.GetRequiredService<ILocationSearchService>();
                await searchService.InitializeAsync();
            }

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

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Flight}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
