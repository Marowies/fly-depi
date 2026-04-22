using Microsoft.EntityFrameworkCore;
using SkyScan.Infrastructure.Data.Data_Sources;
using SkyScan.Application.Interfaces;
using SkyScan.Infrastructure.Services;
using SkyScan.Application.Mappings;
using SkyScan.Presentation.Middlewares;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Infrastructure.Data.Repositories_Implementations;

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

            var app = builder.Build();

            // Seed Data Integration
            // using (var scope = app.Services.CreateScope())
            // {
            //     try 
            //     {
            //         var parentDir = Directory.GetParent(builder.Environment.ContentRootPath)?.FullName ?? builder.Environment.ContentRootPath;
            //         var basePath = Path.Combine(parentDir, "Datasets", "Cleaned");
            //         await SkyScan.Infrastructure.Data.Seeding.DataSeeder.SeedDataAsync(scope.ServiceProvider, basePath);
            //     }
            //     catch (Exception ex)
            //     {
            //         var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            //         logger.LogError(ex, "An error occurred while seeding datasets.");
            //     }
            // }

            // Configure the HTTP request pipeline.
            // 1. Add our Global Exception Handler at the very start of the pipeline
            app.UseMiddleware<GlobalExceptionMiddleware>();

            if (!app.Environment.IsDevelopment())
            {
                // In production, we can also use the default error handler as a fallback
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
