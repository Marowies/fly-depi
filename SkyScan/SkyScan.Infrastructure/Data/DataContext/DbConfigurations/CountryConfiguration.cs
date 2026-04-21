using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyScan.Core.Entities;

namespace SkyScan.Infrastructure.Data.DataContext.DbConfigurations
{
    public class CountryConfiguration : IEntityTypeConfiguration<Country>
    {
        public void Configure(EntityTypeBuilder<Country> builder)
        {
            builder.HasKey(c => c.CountryId);

            builder.Property(c => c.Code)
                .IsRequired()
                .HasMaxLength(2);

            builder.HasIndex(c => c.Code).IsUnique(); 

            builder.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(c => c.Continent)
                .HasMaxLength(10);
        }
    }
}
