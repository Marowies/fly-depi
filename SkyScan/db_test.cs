using System;
using System.Linq;
using SkyScan.Infrastructure.Data.Data_Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IO;

class Program
{
    static void Main()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Path.GetFullPath(@"d:\depi gp\fly-depi\SkyScan\SkyScan.Presentation"))
            .AddJsonFile("appsettings.json", optional: false);
        var config = builder.Build();
        
        var connStr = config.GetConnectionString("SmarterASPNetConnection");
        var optionsBuilder = new DbContextOptionsBuilder<SkyScanDbContext>();
        optionsBuilder.UseSqlServer(connStr);
        
        using var context = new SkyScanDbContext(optionsBuilder.Options);
        
        Console.WriteLine($"Cities count: {context.Cities.Count()}");
        Console.WriteLine($"Airports count: {context.Airports.Count()}");
        Console.WriteLine($"Airlines count: {context.Airlines.Count()}");
        Console.WriteLine($"Airplanes count: {context.Airplanes.Count()}");
    }
}
