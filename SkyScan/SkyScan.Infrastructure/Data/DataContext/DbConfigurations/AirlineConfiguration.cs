using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyScan.Core.Entities.AirLine;

namespace SkyScan.Infrastructure.Data.DataContext.DbConfigurations
{
    public class AirlineConfiguration : IEntityTypeConfiguration<Airline>
    {
        public void Configure(EntityTypeBuilder<Airline> builder)
        {
            builder.HasKey(a => a.AirlineId);

            builder.Property(a => a.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(a => a.HotlineNumber)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(a => a.IcaoCode).HasMaxLength(10);
            builder.Property(a => a.Callsign).HasMaxLength(50);
        }
    }
}
