using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finama.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AjoutAdminLibelle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Libelle",
                table: "Exercices",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 19, 6, 9, 705, DateTimeKind.Utc).AddTicks(3517));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 19, 6, 9, 727, DateTimeKind.Utc).AddTicks(352));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 19, 6, 9, 727, DateTimeKind.Utc).AddTicks(508));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 19, 6, 9, 727, DateTimeKind.Utc).AddTicks(523));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 19, 6, 9, 727, DateTimeKind.Utc).AddTicks(532));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Libelle",
                table: "Exercices");

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
        }
    }
}
