using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finama.Commerce.Migrations
{
    /// <inheritdoc />
    public partial class Images_Produits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImagesProduits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProduitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Contenu = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    TypeMime = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Taille = table.Column<int>(type: "int", nullable: false),
                    JetonPublic = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImagesProduits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImagesProduits_JetonPublic",
                table: "ImagesProduits",
                column: "JetonPublic",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImagesProduits_ProduitId",
                table: "ImagesProduits",
                column: "ProduitId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImagesProduits");
        }
    }
}
