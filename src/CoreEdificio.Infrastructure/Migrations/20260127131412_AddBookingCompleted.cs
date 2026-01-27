using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreEdificio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingCompleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAtUtc",
                table: "Bookings",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "Bookings");
        }
    }
}
