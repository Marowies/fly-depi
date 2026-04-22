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

            builder.HasOne(s => s.OriginAirport)
                .WithMany()
                .HasForeignKey(s => s.OriginAirportId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(s => s.DestinationAirport)
                .WithMany()
                .HasForeignKey(s => s.DestinationAirportId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
