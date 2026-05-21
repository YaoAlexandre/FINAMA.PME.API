using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finama.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Init2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ClassesComptables",
                table: "ClassesComptables");

            migrationBuilder.DropIndex(
                name: "IX_ClassesComptables_TenantId",
                table: "ClassesComptables");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClassesComptables",
                table: "ClassesComptables",
                columns: new[] { "TenantId", "Numero" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ClassesComptables",
                table: "ClassesComptables");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClassesComptables",
                table: "ClassesComptables",
                column: "Numero");

            migrationBuilder.CreateIndex(
                name: "IX_ClassesComptables_TenantId",
                table: "ClassesComptables",
                column: "TenantId");
        }
    }
}
