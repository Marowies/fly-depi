using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyScan.Core.Entities.AirLine;

namespace SkyScan.Infrastructure.Data.DataContext.DbConfigurations
{
    public class AirplaneConfiguration : IEntityTypeConfiguration<Airplane>
    {
        public void Configure(EntityTypeBuilder<Airplane> builder)
        {
            builder.HasKey(a => a.AirplaneId);

            builder.Property(a => a.AircraftCode)
                .IsRequired()
                .HasMaxLength(10);

            builder.Property(a => a.AircraftName)
                .HasMaxLength(150);

            builder.HasIndex(a => a.AircraftCode).IsUnique(false);
        }
    }
}
