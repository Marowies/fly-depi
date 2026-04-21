using Microsoft.EntityFrameworkCore;
using SkyScan.Core.Entities;
using SkyScan.Core.Entities.AirLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkyScan.Infrastructure.Data.Data_Sources
{
    public class SkyScanDbContext : DbContext
    {
        public SkyScanDbContext() : base()
        {
            
        }

        public SkyScanDbContext(DbContextOptions<SkyScanDbContext> options) : base(options)
        {
            
        }

        public DbSet<Airline> Airlines { get; set; }
        public DbSet<Airplane> Airplanes { get; set; }
        public DbSet<Airport> Airports{ get; set; }
        public DbSet<Country> Countries { get; set; }
        public DbSet<City> Cities{ get; set; }
        public DbSet<Flight> Flights{ get; set; }
        public DbSet<PriceAlert> PriceAlerts { get; set; }
        public DbSet<Search> Searches { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<Trip> Trips{ get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(SkyScanDbContext).Assembly);
        }

    }
}
