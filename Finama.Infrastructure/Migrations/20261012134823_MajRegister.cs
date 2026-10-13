using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finama.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MajRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 12, 13, 48, 19, 136, DateTimeKind.Utc).AddTicks(8619));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 12, 13, 48, 19, 170, DateTimeKind.Utc).AddTicks(6370));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 12, 13, 48, 19, 170, DateTimeKind.Utc).AddTicks(6750));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 12, 13, 48, 19, 170, DateTimeKind.Utc).AddTicks(6769));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 12, 13, 48, 19, 170, DateTimeKind.Utc).AddTicks(6797));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 19, 48, 50, 933, DateTimeKind.Utc).AddTicks(2233));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 19, 48, 50, 957, DateTimeKind.Utc).AddTicks(7685));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 19, 48, 50, 957, DateTimeKind.Utc).AddTicks(7817));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 19, 48, 50, 957, DateTimeKind.Utc).AddTicks(7832));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 19, 48, 50, 957, DateTimeKind.Utc).AddTicks(7841));
        }
    }
}
