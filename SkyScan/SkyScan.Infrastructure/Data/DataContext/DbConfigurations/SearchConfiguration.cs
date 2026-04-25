using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyScan.Core.Entities;

namespace SkyScan.Infrastructure.Data.DataContext.DbConfigurations
{
    public class SearchConfiguration : IEntityTypeConfiguration<Search>
    {
        public void Configure(EntityTypeBuilder<Search> builder)
        {
            builder.HasKey(s => s.SearchId);

            builder.Property(s => s.TimeStamp).IsRequired();
            builder.Property(s => s.DepartureDate).IsRequired();

            builder.HasOne(s => s.User)
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
