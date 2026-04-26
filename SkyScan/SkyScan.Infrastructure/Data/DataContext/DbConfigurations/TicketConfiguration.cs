using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyScan.Core.Entities.AirLine;

namespace SkyScan.Infrastructure.Data.DataContext.DbConfigurations
{
    public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
    {
        public void Configure(EntityTypeBuilder<Ticket> builder)
        {
            builder.HasKey(t => t.TicketId);

            builder.Property(t => t.Price)
                .HasColumnType("decimal(18,2)");

            builder.Property(t => t.LuggageDescription)
                .HasMaxLength(100);

            builder.HasOne(t => t.Flight)
                .WithMany(f => f.Tickets)
                .HasForeignKey(t => t.FlightId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
