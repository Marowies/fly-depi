using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SkyScan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SchemaRefactor_v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Airplanes_Airlines_AirlineId",
                table: "Airplanes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Searches",
                table: "Searches");

            migrationBuilder.DropIndex(
                name: "IX_Searches_UserId",
                table: "Searches");

            migrationBuilder.DropIndex(
                name: "IX_Airplanes_AirlineId",
                table: "Airplanes");

            migrationBuilder.DropIndex(
                name: "IX_Airplanes_Icao24",
                table: "Airplanes");

            migrationBuilder.DropIndex(
                name: "IX_Airplanes_PlaneId",
                table: "Airplanes");

            migrationBuilder.DropIndex(
                name: "IX_Airplanes_Registration",
                table: "Airplanes");

            migrationBuilder.DropColumn(
                name: "LuggageDescription",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "LuggageWeight",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SearchId",
                table: "Searches");

            migrationBuilder.DropColumn(
                name: "AirlineId",
                table: "Airplanes");

            migrationBuilder.DropColumn(
                name: "CabinClasses",
                table: "Airplanes");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Airplanes");

            migrationBuilder.DropColumn(
                name: "EngineType",
                table: "Airplanes");

            migrationBuilder.DropColumn(
                name: "Icao24",
                table: "Airplanes");

            migrationBuilder.DropColumn(
                name: "ManufactureCompany",
                table: "Airplanes");

            migrationBuilder.DropColumn(
                name: "ManufactureDate",
                table: "Airplanes");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "Airplanes");

            migrationBuilder.DropColumn(
                name: "OwnerCompany",
                table: "Airplanes");

            migrationBuilder.DropColumn(
                name: "PlaneId",
                table: "Airplanes");

            migrationBuilder.DropColumn(
                name: "Registration",
                table: "Airplanes");

            migrationBuilder.DropColumn(
                name: "Seats",
                table: "Airplanes");

            migrationBuilder.DropColumn(
                name: "SerialNumber",
                table: "Airplanes");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Airplanes");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Airplanes");

            migrationBuilder.DropColumn(
                name: "Callsign",
                table: "Airlines");

            migrationBuilder.DropColumn(
                name: "IcaoCode",
                table: "Airlines");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Tickets",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SearchCount",
                table: "Cities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AircraftCode",
                table: "Airplanes",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AircraftName",
                table: "Airplanes",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IataCode",
                table: "Airlines",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "HotlineNumber",
                table: "Airlines",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Searches",
                table: "Searches",
                columns: new[] { "UserId", "OriginCityId", "DestinationCityId" });

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "11111111-1111-1111-1111-111111111111", "Guest", "GUEST" },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "22222222-2222-2222-2222-222222222222", "RegisteredUser", "REGISTEREDUSER" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Airplanes_AircraftCode",
                table: "Airplanes",
                column: "AircraftCode");

            migrationBuilder.CreateIndex(
                name: "IX_Airlines_IataCode",
                table: "Airlines",
                column: "IataCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Searches",
                table: "Searches");

            migrationBuilder.DropIndex(
                name: "IX_Airplanes_AircraftCode",
                table: "Airplanes");

            migrationBuilder.DropIndex(
                name: "IX_Airlines_IataCode",
                table: "Airlines");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"));

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SearchCount",
                table: "Cities");

            migrationBuilder.DropColumn(
                name: "AircraftCode",
                table: "Airplanes");

            migrationBuilder.DropColumn(
                name: "AircraftName",
                table: "Airplanes");

            migrationBuilder.AddColumn<string>(
                name: "LuggageDescription",
                table: "Tickets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LuggageWeight",
                table: "Tickets",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SearchId",
                table: "Searches",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AirlineId",
                table: "Airplanes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CabinClasses",
                table: "Airplanes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                table: "Airplanes",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EngineType",
                table: "Airplanes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Icao24",
                table: "Airplanes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManufactureCompany",
                table: "Airplanes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ManufactureDate",
                table: "Airplanes",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "Airplanes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerCompany",
                table: "Airplanes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PlaneId",
                table: "Airplanes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Registration",
                table: "Airplanes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Seats",
                table: "Airplanes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SerialNumber",
                table: "Airplanes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                table: "Airplanes",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Airplanes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IataCode",
                table: "Airlines",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "HotlineNumber",
                table: "Airlines",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Callsign",
                table: "Airlines",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IcaoCode",
                table: "Airlines",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Searches",
                table: "Searches",
                column: "SearchId");

            migrationBuilder.CreateIndex(
                name: "IX_Searches_UserId",
                table: "Searches",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Airplanes_AirlineId",
                table: "Airplanes",
                column: "AirlineId");

            migrationBuilder.CreateIndex(
                name: "IX_Airplanes_Icao24",
                table: "Airplanes",
                column: "Icao24");

            migrationBuilder.CreateIndex(
                name: "IX_Airplanes_PlaneId",
                table: "Airplanes",
                column: "PlaneId");

            migrationBuilder.CreateIndex(
                name: "IX_Airplanes_Registration",
                table: "Airplanes",
                column: "Registration");

            migrationBuilder.AddForeignKey(
                name: "FK_Airplanes_Airlines_AirlineId",
                table: "Airplanes",
                column: "AirlineId",
                principalTable: "Airlines",
                principalColumn: "AirlineId");
        }
    }
}
