using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SkyScan.Core.Entities;
using SkyScan.Core.Entities.AirLine;
using SkyScan.Infrastructure.Identity;
using System;

namespace SkyScan.Infrastructure.Data.Data_Sources
{
    public class SkyScanDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
    {
        public SkyScanDbContext() : base()
        {
        }

        public SkyScanDbContext(DbContextOptions<SkyScanDbContext> options) : base(options)
        {
        }

        public DbSet<Airline> Airlines { get; set; }
        public DbSet<Airplane> Airplanes { get; set; }
        public DbSet<Airport> Airports { get; set; }
        public DbSet<Country> Countries { get; set; }
        public DbSet<City> Cities { get; set; }
        public DbSet<Flight> Flights { get; set; }
        public DbSet<PriceAlert> PriceAlerts { get; set; }
        public DbSet<Search> Searches { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<Trip> Trips { get; set; }
        public DbSet<ApplicationUser> Users { get; set; }
        public DbSet<Booking> Bookings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(SkyScanDbContext).Assembly);

            // ── Role Seeds ────────────────────────────────────────────────────────
            var guestRoleId = new Guid("11111111-1111-1111-1111-111111111111");
            var registeredUserRoleId = new Guid("22222222-2222-2222-2222-222222222222");

            modelBuilder.Entity<IdentityRole<Guid>>().HasData(
                new IdentityRole<Guid>
                {
                    Id = guestRoleId,
                    Name = "Guest",
                    NormalizedName = "GUEST",
                    ConcurrencyStamp = guestRoleId.ToString()
                },
                new IdentityRole<Guid>
                {
                    Id = registeredUserRoleId,
                    Name = "RegisteredUser",
                    NormalizedName = "REGISTEREDUSER",
                    ConcurrencyStamp = registeredUserRoleId.ToString()
                }
            );
        }
    }
}
