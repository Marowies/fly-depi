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

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            // IMemoryCache: used to cache static reference data (e.g. airport dropdown list)
            builder.Services.AddMemoryCache();

            // DbContext should be Scoped (one per request), not Transient (wastes connection pool)
            builder.Services.AddDbContext<SkyScanDbContext>(options =>
                    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
            
            // Add AutoMapper
            builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());
 
            // Add Identity
            builder.Services.AddIdentity<User, IdentityRole<Guid>>()
                .AddEntityFrameworkStores<SkyScanDbContext>()
                .AddDefaultTokenProviders();

            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Account/AccessDenied";
            });
 
            // Register Repositories
            builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            builder.Services.AddScoped<IFlightRepository, FlightRepository>();
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IPriceAlertRepository, PriceAlertRepository>();
            builder.Services.AddScoped<ISearchRepository, SearchRepository>();
            builder.Services.AddScoped<IAirportRepository, AirportRepository>();
 
            var useMockData = builder.Configuration["FlightProviderSettings:UseMockData"] == "true";

            if (useMockData)
            {
                builder.Services.AddScoped<IFlightProviderService, MockFlightProviderService>();
            }
            else
            {
                builder.Services.AddHttpClient<IFlightProviderService, AviationStackFlightService>();
            }

            builder.Services.AddSingleton<ILocationSearchService, LocationSearchService>();
            builder.Services.AddScoped<IFlightFilteringService, FlightFilteringService>();

            var app = builder.Build();

            // Warm up the In-Memory Search Index
            using (var scope = app.Services.CreateScope())
            {
                var searchService = scope.ServiceProvider.GetRequiredService<ILocationSearchService>();
                await searchService.InitializeAsync();
            }

            // Configure the HTTP request pipeline.
            // 1. Add our Global Exception Handler at the very start of the pipeline
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
