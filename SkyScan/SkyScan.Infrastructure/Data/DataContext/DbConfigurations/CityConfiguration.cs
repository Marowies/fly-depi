using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyScan.Core.Entities;

namespace SkyScan.Infrastructure.Data.DataContext.DbConfigurations
{
    public class CityConfiguration : IEntityTypeConfiguration<City>
    {
        public void Configure(EntityTypeBuilder<City> builder)
        {
            builder.HasKey(c => c.CityId);

            builder.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(c => c.CountryCode)
                .IsRequired()
                .HasMaxLength(2);

            builder.HasOne(c => c.Country)
                .WithMany(co => co.Cities)
                .HasForeignKey(c => c.CountryCode)
                .HasPrincipalKey(co => co.Code)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
