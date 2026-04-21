using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SkyScan.Core.Constants;
using SkyScan.Core.Entities.AirLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SkyScan.Infrastructure.Data.DataContext.DbConfigurations
{
    public class AirplaneConfiguration : IEntityTypeConfiguration<Airplane>
    {
        public void Configure(EntityTypeBuilder<Airplane> builder)
        {
            builder.HasKey(a => a.AirplaneId);

            builder.Property(a => a.Model)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(a => a.PlaneId)
                .IsRequired()
                .HasMaxLength(50);

            builder.HasIndex(a => a.PlaneId);
            builder.HasIndex(a => a.Registration);
            builder.HasIndex(a => a.Icao24);

            builder.Property(a => a.Icao24).HasMaxLength(20);
            builder.Property(a => a.Registration).HasMaxLength(20);
            builder.Property(a => a.SerialNumber).HasMaxLength(100);
            builder.Property(a => a.EngineType).HasMaxLength(50);
            builder.Property(a => a.Status).HasMaxLength(20);

            // Value Converter for List<CabinType> using JSON
            builder.Property(a => a.CabinClasses)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<CabinType>>(v, (JsonSerializerOptions?)null) ?? new List<CabinType>()
                )
                .Metadata.SetValueComparer(new ValueComparer<List<CabinType>>(
                    (c1, c2) => (c1 != null && c2 != null) && c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()
                ));
        }
    }
}
