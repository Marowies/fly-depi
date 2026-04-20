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
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddDbContext<SkyScanDbContext>(options => 
                    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
            
            // Add AutoMapper
            builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);

            var useMockData = builder.Configuration.GetValue<bool>("FlightProviderSettings:UseMockData");

            if (useMockData)
            {
                builder.Services.AddScoped<IFlightProviderService, MockFlightProviderService>();
            }
            else
            {
                builder.Services.AddHttpClient<IFlightProviderService, AviationStackFlightService>();
            }

            var app = builder.Build();

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
