using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreEdificio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserUnitsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserUnits",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelationshipType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserUnits", x => new { x.UserId, x.UnitId });
                    table.ForeignKey(
                        name: "FK_UserUnits_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserUnits_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserUnits_CommunityId",
                table: "UserUnits",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_UserUnits_UnitId",
                table: "UserUnits",
                column: "UnitId");

            // Backfill data from AspNetUsers to UserUnits
            migrationBuilder.Sql(@"
                INSERT INTO UserUnits (UserId, UnitId, CommunityId, RelationshipType, IsPrimary, CreatedAtUtc)
                SELECT Id, UnitId, CommunityId, 'Resident', 1, GETUTCDATE()
                FROM AspNetUsers
                WHERE UnitId IS NOT NULL AND CommunityId IS NOT NULL
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserUnits");
        }
    }
}
