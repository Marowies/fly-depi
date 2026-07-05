using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkyScan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAirlineUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Url",
                table: "Airlines",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
          
            migrationBuilder.DropColumn(
                name: "Url",
                table: "Airlines");

        }
    }
}
