using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkyScan.Infrastructure.Data.Migrations
{
    /// <summary>
    /// PARKED — do not apply. This migration was drafted to make PriceAlert self-contained
    /// (dropping the TripId FK and adding route/flight snapshot columns), but the target
    /// database is live and the change was deferred to avoid schema risk. Both Up and Down
    /// are intentionally no-ops so this migration is harmless if it's ever run by mistake;
    /// PriceAlert still uses its original Trip-based shape. Delete this file (and the
    /// matching .Designer.cs) once you're ready to revisit the self-contained design, or
    /// re-derive it properly via `dotnet ef migrations add` at that point.
    /// </summary>
    public partial class SelfContainedPriceAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty — parked, see class remarks.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty — parked, see class remarks.
        }
    }
}
