using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finama.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Ventes_Devis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Devis_TenantId_Numero",
                table: "Devis");

            migrationBuilder.AlterColumn<decimal>(
                name: "TauxTVA",
                table: "LignesDevis",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantite",
                table: "LignesDevis",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<int>(
                name: "Ordre",
                table: "LignesDevis",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ProduitId",
                table: "LignesDevis",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RemisePourcent",
                table: "LignesDevis",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "DevisId",
                table: "Factures",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "Devis",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Libelle",
                table: "Devis",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300);

            migrationBuilder.AddColumn<string>(
                name: "Conditions",
                table: "Devis",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateDecision",
                table: "Devis",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateEnvoi",
                table: "Devis",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DevisOrigineId",
                table: "Devis",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FactureId",
                table: "Devis",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FactureNumero",
                table: "Devis",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotifRefus",
                table: "Devis",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RemiseGlobalePourcent",
                table: "Devis",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Devis",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 17, 12, 47, 10, 653, DateTimeKind.Utc).AddTicks(3710));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 17, 12, 47, 10, 675, DateTimeKind.Utc).AddTicks(4563));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 17, 12, 47, 10, 675, DateTimeKind.Utc).AddTicks(4712));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 17, 12, 47, 10, 675, DateTimeKind.Utc).AddTicks(4723));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 17, 12, 47, 10, 675, DateTimeKind.Utc).AddTicks(4729));

            migrationBuilder.CreateIndex(
                name: "IX_Devis_TenantId_Numero",
                table: "Devis",
                columns: new[] { "TenantId", "Numero" });

            migrationBuilder.CreateIndex(
                name: "IX_Devis_TenantId_Statut",
                table: "Devis",
                columns: new[] { "TenantId", "Statut" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Devis_TenantId_Numero",
                table: "Devis");

            migrationBuilder.DropIndex(
                name: "IX_Devis_TenantId_Statut",
                table: "Devis");

            migrationBuilder.DropColumn(
                name: "Ordre",
                table: "LignesDevis");

            migrationBuilder.DropColumn(
                name: "ProduitId",
                table: "LignesDevis");

            migrationBuilder.DropColumn(
                name: "RemisePourcent",
                table: "LignesDevis");

            migrationBuilder.DropColumn(
                name: "DevisId",
                table: "Factures");

            migrationBuilder.DropColumn(
                name: "Conditions",
                table: "Devis");

            migrationBuilder.DropColumn(
                name: "DateDecision",
                table: "Devis");

            migrationBuilder.DropColumn(
                name: "DateEnvoi",
                table: "Devis");

            migrationBuilder.DropColumn(
                name: "DevisOrigineId",
                table: "Devis");

            migrationBuilder.DropColumn(
                name: "FactureId",
                table: "Devis");

            migrationBuilder.DropColumn(
                name: "FactureNumero",
                table: "Devis");

            migrationBuilder.DropColumn(
                name: "MotifRefus",
                table: "Devis");

            migrationBuilder.DropColumn(
                name: "RemiseGlobalePourcent",
                table: "Devis");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Devis");

            migrationBuilder.AlterColumn<decimal>(
                name: "TauxTVA",
                table: "LignesDevis",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldPrecision: 5,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantite",
                table: "LignesDevis",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "Devis",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Libelle",
                table: "Devis",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 17, 7, 33, 58, 742, DateTimeKind.Utc).AddTicks(3220));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 17, 7, 33, 58, 763, DateTimeKind.Utc).AddTicks(5581));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 17, 7, 33, 58, 763, DateTimeKind.Utc).AddTicks(5687));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 17, 7, 33, 58, 763, DateTimeKind.Utc).AddTicks(5700));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 17, 7, 33, 58, 763, DateTimeKind.Utc).AddTicks(5708));

            migrationBuilder.CreateIndex(
                name: "IX_Devis_TenantId_Numero",
                table: "Devis",
                columns: new[] { "TenantId", "Numero" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");
        }
    }
}
