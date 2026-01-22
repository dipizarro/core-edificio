using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreEdificio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChargesAndBookingAudits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Booking_Facilities_FacilityId",
                table: "Booking");

            migrationBuilder.DropForeignKey(
                name: "FK_Booking_Units_UnitId",
                table: "Booking");

            migrationBuilder.RenameTable(
                name: "Booking",
                newName: "Bookings");

            migrationBuilder.AlterColumn<Guid>(
                name: "UnitId",
                table: "Bookings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "FacilityId",
                table: "Bookings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "Bookings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAtUtc",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                table: "Bookings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "Bookings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAtUtc",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancelledByUserId",
                table: "Bookings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CommunityId",
                table: "Bookings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Bookings",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Bookings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "EndAtUtc",
                table: "Bookings",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Bookings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectReason",
                table: "Bookings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartAtUtc",
                table: "Bookings",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Bookings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Bookings",
                table: "Bookings",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Charges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Period = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceRef = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ChargeKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Charges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CommunityId_FacilityId_StartAtUtc_EndAtUtc",
                table: "Bookings",
                columns: new[] { "CommunityId", "FacilityId", "StartAtUtc", "EndAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_FacilityId",
                table: "Bookings",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_UnitId",
                table: "Bookings",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_CommunityId_UnitId_Period",
                table: "Charges",
                columns: new[] { "CommunityId", "UnitId", "Period" });

            migrationBuilder.CreateIndex(
                name: "IX_Charges_SourceType_SourceId_ChargeKind",
                table: "Charges",
                columns: new[] { "SourceType", "SourceId", "ChargeKind" },
                unique: true,
                filter: "[SourceId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Facilities_FacilityId",
                table: "Bookings",
                column: "FacilityId",
                principalTable: "Facilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Units_UnitId",
                table: "Bookings",
                column: "UnitId",
                principalTable: "Units",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Facilities_FacilityId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Units_UnitId",
                table: "Bookings");

            migrationBuilder.DropTable(
                name: "Charges");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Bookings",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_CommunityId_FacilityId_StartAtUtc_EndAtUtc",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_FacilityId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_UnitId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ApprovedAtUtc",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CancelledAtUtc",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CancelledByUserId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CommunityId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "EndAtUtc",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "RejectReason",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "StartAtUtc",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Bookings");

            migrationBuilder.RenameTable(
                name: "Bookings",
                newName: "Booking");

            migrationBuilder.AlterColumn<Guid>(
                name: "UnitId",
                table: "Booking",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "FacilityId",
                table: "Booking",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddForeignKey(
                name: "FK_Booking_Facilities_FacilityId",
                table: "Booking",
                column: "FacilityId",
                principalTable: "Facilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Booking_Units_UnitId",
                table: "Booking",
                column: "UnitId",
                principalTable: "Units",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
