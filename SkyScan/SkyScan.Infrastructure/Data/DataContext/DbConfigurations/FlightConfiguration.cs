using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyScan.Core.Entities.AirLine;

namespace SkyScan.Infrastructure.Data.DataContext.DbConfigurations
{
    public class FlightConfiguration : IEntityTypeConfiguration<Flight>
    {
        public void Configure(EntityTypeBuilder<Flight> builder)
        {
            builder.HasKey(f => f.FlightId);

            builder.Property(f => f.FlightNumber)
                .IsRequired()
                .HasMaxLength(20);

            builder.HasOne(f => f.Airline)
                .WithMany(a => a.Flights)
                .HasForeignKey(f => f.AirlineId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(f => f.Airplane)
                .WithMany()
                .HasForeignKey(f => f.AirplaneId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(f => f.DepartureAirport)
                .WithMany()
                .HasForeignKey(f => f.DepartureAirportId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(f => f.ArrivalAirport)
                .WithMany()
                .HasForeignKey(f => f.ArrivalAirportId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
