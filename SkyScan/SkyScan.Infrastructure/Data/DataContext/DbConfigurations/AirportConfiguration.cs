using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyScan.Core.Entities;

namespace SkyScan.Infrastructure.Data.DataContext.DbConfigurations
{
    public class AirportConfiguration : IEntityTypeConfiguration<Airport>
    {
        public void Configure(EntityTypeBuilder<Airport> builder)
        {
            builder.HasKey(a => a.AirportId);

            builder.Property(a => a.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.HasIndex(a => a.Code);
            builder.HasIndex(a => a.IataCode);
            builder.HasIndex(a => a.IcaoCode);

            builder.Property(a => a.Code)
                .IsRequired()
                .HasMaxLength(10);

            builder.Property(a => a.IataCode).HasMaxLength(10);
            builder.Property(a => a.IcaoCode).HasMaxLength(10);
            builder.Property(a => a.Type).HasMaxLength(50);

            builder.HasOne(a => a.City)
                .WithMany(c => c.Airports)
                .HasForeignKey(a => a.CityId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
