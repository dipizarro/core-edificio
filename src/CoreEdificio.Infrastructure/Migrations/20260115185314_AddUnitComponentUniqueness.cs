using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreEdificio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitComponentUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM UnitComponents
                WHERE Id NOT IN (
                    SELECT MIN(Id)
                    FROM UnitComponents
                    GROUP BY CommunityId, [Type], Code
                );
            ");

            migrationBuilder.CreateIndex(
                name: "IX_UnitComponents_CommunityId_Type_Code",
                table: "UnitComponents",
                columns: new[] { "CommunityId", "Type", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UnitComponents_CommunityId_Type_Code",
                table: "UnitComponents");
        }
    }
}
