using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyScan.Core.Entities;
using SkyScan.Infrastructure.Identity;

namespace SkyScan.Infrastructure.Data.DataContext.DbConfigurations
{
    public class SearchConfiguration : IEntityTypeConfiguration<Search>
    {
        public void Configure(EntityTypeBuilder<Search> builder)
        {
            // Composite PK: one record per unique route per user
            builder.HasKey(s => new { s.UserId, s.OriginCityId, s.DestinationCityId });

            builder.Property(s => s.TimeStamp).IsRequired();
            builder.Property(s => s.DepartureDate).IsRequired();

            // FK-only — Core.Search doesn't (and shouldn't) hold a navigation to the
            // Infrastructure-owned ApplicationUser; the collection nav lives on ApplicationUser.
            builder.HasOne<ApplicationUser>()
                .WithMany(u => u.Searches)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(s => s.OriginCity)
                .WithMany()
                .HasForeignKey(s => s.OriginCityId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(s => s.DestinationCity)
                .WithMany()
                .HasForeignKey(s => s.DestinationCityId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
