using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finama.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AjoutAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Utilisateurs_TenantId_Email",
                table: "Utilisateurs");

            migrationBuilder.DropIndex(
                name: "IX_Factures_TenantId_Numero",
                table: "Factures");

            migrationBuilder.DropIndex(
                name: "IX_Ecritures_TenantId_Reference",
                table: "Ecritures");

            migrationBuilder.DropIndex(
                name: "IX_Devis_TenantId_Numero",
                table: "Devis");

            migrationBuilder.DropIndex(
                name: "IX_CompteComptables_TenantId_Numero",
                table: "CompteComptables");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Utilisateurs",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Tiers",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<bool>(
                name: "Actif",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateCreation",
                table: "Tenants",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "LignesFacture",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "LignesEcriture",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Factures",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Exercices",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Ecritures",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Devis",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "CompteComptables",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 15, 27, 24, 347, DateTimeKind.Utc).AddTicks(7559));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 15, 27, 24, 367, DateTimeKind.Utc).AddTicks(6991));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 15, 27, 24, 367, DateTimeKind.Utc).AddTicks(7171));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 15, 27, 24, 367, DateTimeKind.Utc).AddTicks(7184));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 15, 27, 24, 367, DateTimeKind.Utc).AddTicks(7193));

            migrationBuilder.CreateIndex(
                name: "IX_Utilisateurs_TenantId_Email",
                table: "Utilisateurs",
                columns: new[] { "TenantId", "Email" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Factures_TenantId_Numero",
                table: "Factures",
                columns: new[] { "TenantId", "Numero" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Ecritures_TenantId_Reference",
                table: "Ecritures",
                columns: new[] { "TenantId", "Reference" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Devis_TenantId_Numero",
                table: "Devis",
                columns: new[] { "TenantId", "Numero" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CompteComptables_TenantId_Numero",
                table: "CompteComptables",
                columns: new[] { "TenantId", "Numero" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Utilisateurs_TenantId_Email",
                table: "Utilisateurs");

            migrationBuilder.DropIndex(
                name: "IX_Factures_TenantId_Numero",
                table: "Factures");

            migrationBuilder.DropIndex(
                name: "IX_Ecritures_TenantId_Reference",
                table: "Ecritures");

            migrationBuilder.DropIndex(
                name: "IX_Devis_TenantId_Numero",
                table: "Devis");

            migrationBuilder.DropIndex(
                name: "IX_CompteComptables_TenantId_Numero",
                table: "CompteComptables");

            migrationBuilder.DropColumn(
                name: "Actif",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DateCreation",
                table: "Tenants");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Utilisateurs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Tiers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "LignesFacture",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "LignesEcriture",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Factures",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Exercices",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Ecritures",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "Devis",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                table: "CompteComptables",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 14, 46, 52, 39, DateTimeKind.Utc).AddTicks(5690));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 14, 46, 52, 59, DateTimeKind.Utc).AddTicks(6284));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 14, 46, 52, 59, DateTimeKind.Utc).AddTicks(6419));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 14, 46, 52, 59, DateTimeKind.Utc).AddTicks(6432));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 14, 46, 52, 59, DateTimeKind.Utc).AddTicks(6440));

            migrationBuilder.CreateIndex(
                name: "IX_Utilisateurs_TenantId_Email",
                table: "Utilisateurs",
                columns: new[] { "TenantId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Factures_TenantId_Numero",
                table: "Factures",
                columns: new[] { "TenantId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ecritures_TenantId_Reference",
                table: "Ecritures",
                columns: new[] { "TenantId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devis_TenantId_Numero",
                table: "Devis",
                columns: new[] { "TenantId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompteComptables_TenantId_Numero",
                table: "CompteComptables",
                columns: new[] { "TenantId", "Numero" },
                unique: true);
        }
    }
}
