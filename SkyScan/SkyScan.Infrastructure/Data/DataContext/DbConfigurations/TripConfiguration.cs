using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyScan.Core.Entities;

namespace SkyScan.Infrastructure.Data.DataContext.DbConfigurations
{
    public class TripConfiguration : IEntityTypeConfiguration<Trip>
    {
        public void Configure(EntityTypeBuilder<Trip> builder)
        {
            builder.HasKey(t => t.TripId);

            builder.Property(t => t.TotalPrice)
                .IsRequired();

            builder.Property(t => t.Stops)
                .IsRequired();
        }
    }
}
