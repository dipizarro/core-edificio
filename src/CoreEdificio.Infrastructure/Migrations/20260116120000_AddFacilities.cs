using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreEdificio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFacilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Facilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: true),
                    ChargingMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RentAmountClp = table.Column<int>(type: "int", nullable: false),
                    DepositAmountClp = table.Column<int>(type: "int", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    SlotDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    MaxHoursPerBooking = table.Column<int>(type: "int", nullable: true),
                    MaxBookingsPerMonthPerUnit = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Facilities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Facilities_CommunityId_Name",
                table: "Facilities",
                columns: new[] { "CommunityId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Facilities");
        }
    }
}
