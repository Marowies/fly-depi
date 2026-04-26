using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyScan.Core.Entities;

namespace SkyScan.Infrastructure.Data.DataContext.DbConfigurations
{
    public class CountryConfiguration : IEntityTypeConfiguration<Country>
    {
        public void Configure(EntityTypeBuilder<Country> builder)
        {
            builder.HasKey(c => c.CountryCode);

            builder.Property(c => c.CountryCode)
                .IsRequired()
                .HasMaxLength(2);

            builder.HasIndex(c => c.CountryCode).IsUnique(); 

            builder.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(c => c.Continent)
                .HasMaxLength(10);
        }
    }
}
