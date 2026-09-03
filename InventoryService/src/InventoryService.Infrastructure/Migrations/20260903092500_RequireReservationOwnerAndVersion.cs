using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RequireReservationOwnerAndVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockReservations_InventoryItemId_ReservationRequestId",
                table: "StockReservations");

            migrationBuilder.AlterColumn<Guid>(
                name: "InventoryItemId",
                table: "StockReservations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "ReservationAttempts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_InventoryItemId_ReservationRequestId",
                table: "StockReservations",
                columns: new[] { "InventoryItemId", "ReservationRequestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockReservations_InventoryItemId_ReservationRequestId",
                table: "StockReservations");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "ReservationAttempts");

            migrationBuilder.AlterColumn<Guid>(
                name: "InventoryItemId",
                table: "StockReservations",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_InventoryItemId_ReservationRequestId",
                table: "StockReservations",
                columns: new[] { "InventoryItemId", "ReservationRequestId" },
                unique: true,
                filter: "[InventoryItemId] IS NOT NULL");
        }
    }
}
