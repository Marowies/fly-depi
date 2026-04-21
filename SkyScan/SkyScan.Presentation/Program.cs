using Microsoft.EntityFrameworkCore;
using SkyScan.Infrastructure.Data.Data_Sources;
using SkyScan.Application.Interfaces;
using SkyScan.Infrastructure.Services;
using SkyScan.Application.Mappings;
using SkyScan.Presentation.Middlewares;
namespace SkyScan.Presentation
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddDbContext<SkyScanDbContext>(options => 
                    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")),
                    ServiceLifetime.Transient);
            
            // Add AutoMapper
            builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());
 
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
            using (var scope = app.Services.CreateScope())
            {
                try 
                {
                    var parentDir = Directory.GetParent(builder.Environment.ContentRootPath)?.FullName ?? builder.Environment.ContentRootPath;
                    var basePath = Path.Combine(parentDir, "Datasets", "Cleaned");
                    await SkyScan.Infrastructure.Data.Seeding.DataSeeder.SeedDataAsync(scope.ServiceProvider, basePath);
                }
                catch (Exception ex)
                {
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "An error occurred while seeding datasets.");
                }
            }

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
