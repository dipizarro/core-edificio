using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreEdificio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitComponents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UnitComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CoefficientPct = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitComponents_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnitComponents_CommunityId_UnitId",
                table: "UnitComponents",
                columns: new[] { "CommunityId", "UnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_UnitComponents_UnitId_Type_Code",
                table: "UnitComponents",
                columns: new[] { "UnitId", "Type", "Code" },
                unique: true);

            migrationBuilder.Sql(@"
                INSERT INTO UnitComponents (Id, CommunityId, UnitId, Type, Code, CoefficientPct, IsActive, CreatedAtUtc)
                SELECT NEWID(), CommunityId, Id, 'Department', Number, CoefficientPct, 1, GETUTCDATE()
                FROM Units
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnitComponents");
        }
    }
}
