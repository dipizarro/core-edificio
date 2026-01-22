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
            migrationBuilder.DropIndex(
                name: "IX_Charges_SourceType_SourceId_ChargeKind",
                table: "Charges");

            migrationBuilder.AlterColumn<string>(
                name: "FineType",
                table: "Charges",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

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
            migrationBuilder.DropIndex(
                name: "IX_Charges_SourceType_SourceId_ChargeKind_FineType",
                table: "Charges");

            migrationBuilder.AlterColumn<string>(
                name: "FineType",
                table: "Charges",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Charges_SourceType_SourceId_ChargeKind",
                table: "Charges",
                columns: new[] { "SourceType", "SourceId", "ChargeKind" },
                unique: true,
                filter: "[SourceId] IS NOT NULL");
        }
    }
}
