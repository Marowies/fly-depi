using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyScan.Core.Entities;

namespace SkyScan.Infrastructure.Data.DataContext.DbConfigurations
{
    public class PriceAlertConfiguration : IEntityTypeConfiguration<PriceAlert>
    {
        public void Configure(EntityTypeBuilder<PriceAlert> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.TargetPrice)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            builder.HasOne(p => p.User)
                .WithMany(u => u.PriceAlerts)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(p => p.Trip)
                .WithMany()
                .HasForeignKey(p => p.TripId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
