using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreEdificio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingFines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CancelPenaltyHours",
                table: "Facilities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LateCancelFineAmountClp",
                table: "Facilities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NoShowFineAmountClp",
                table: "Facilities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropIndex(
                name: "IX_Charges_SourceType_SourceId_ChargeKind",
                table: "Charges");

            migrationBuilder.AddColumn<string>(
                name: "FineType",
                table: "Charges",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Charges_SourceType_SourceId_ChargeKind_FineType",
                table: "Charges",
                columns: new[] { "SourceType", "SourceId", "ChargeKind", "FineType" },
                unique: true,
                filter: "[SourceId] IS NOT NULL AND [FineType] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelPenaltyHours",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "LateCancelFineAmountClp",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "NoShowFineAmountClp",
                table: "Facilities");

            migrationBuilder.DropIndex(
                name: "IX_Charges_SourceType_SourceId_ChargeKind_FineType",
                table: "Charges");

            migrationBuilder.DropColumn(
                name: "FineType",
                table: "Charges");

            migrationBuilder.CreateIndex(
                name: "IX_Charges_SourceType_SourceId_ChargeKind",
                table: "Charges",
                columns: new[] { "SourceType", "SourceId", "ChargeKind" },
                unique: true,
                filter: "[SourceId] IS NOT NULL");
        }
    }
}
