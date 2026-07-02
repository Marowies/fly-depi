using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyScan.Core.Entities;
using SkyScan.Infrastructure.Identity;

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

            // FK-only — Core.PriceAlert doesn't hold a navigation to the Infrastructure-owned
            // ApplicationUser; the collection nav lives on ApplicationUser.
            builder.HasOne<ApplicationUser>()
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
