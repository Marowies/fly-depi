using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyScan.Core.Entities;
using SkyScan.Infrastructure.Identity;

namespace SkyScan.Infrastructure.Data.DataContext.DbConfigurations
{
    public class BookingConfiguration : IEntityTypeConfiguration<Booking>
    {
        public void Configure(EntityTypeBuilder<Booking> builder)
        {
            builder.HasKey(b => b.BookingId);

            // FK-only — Core.Booking doesn't hold a navigation to the Infrastructure-owned
            // ApplicationUser. This relationship used to be picked up by convention through
            // Booking.User; now that the nav is gone it needs to be explicit.
            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(b => b.Flight)
                .WithMany()
                .HasForeignKey(b => b.FlightId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
