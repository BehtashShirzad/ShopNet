using Domain.Abstractions;
using InventoryService.Domain.Enums;

namespace InventoryService.UnitTest;

public sealed class InventoryItemEdgeCaseTests
{
    [Fact]
    public void Reserve_ConvenienceOverload_UsesCurrentUtcTime()
    {
        var inventory = InventoryTestData.CreateInventory();
        var before = DateTime.UtcNow;
        var expiresAt = before.AddHours(1);

        var reservation = inventory.Reserve(
            Guid.NewGuid(),
            1,
            expiresAt);

        var after = DateTime.UtcNow;
        Assert.InRange(reservation.ReservedAtUtc, before, after);
        Assert.Equal(expiresAt, reservation.ExpiresAtUtc);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_IsIdempotent()
    {
        var inventory = InventoryTestData.CreateInventory();
        inventory.Deactivate();
        inventory.ClearEvents();

        inventory.Deactivate();

        Assert.False(inventory.IsActive);
        Assert.Empty(inventory.DomainEvents);
    }

    [Fact]
    public void ReservationOperation_WithEmptyOrderId_Throws()
    {
        var inventory = InventoryTestData.CreateInventory();

        Assert.Throws<DomainException>(() =>
            inventory.CommitReservation(
                Guid.Empty,
                InventoryTestData.ReservedAtUtc));
    }

    [Fact]
    public void ReceiveStock_WhenQuantityOverflows_LeavesStateUnchanged()
    {
        var inventory = InventoryTestData.CreateInventory(
            initialQuantity: int.MaxValue);

        Assert.Throws<OverflowException>(
            () => inventory.ReceiveStock(1, Guid.NewGuid()));

        Assert.Equal(int.MaxValue, inventory.OnHandQuantity);
        Assert.Empty(inventory.DomainEvents);
    }

    [Fact]
    public void AdjustStock_WhenQuantityOverflows_LeavesStateUnchanged()
    {
        var inventory = InventoryTestData.CreateInventory(
            initialQuantity: int.MaxValue);

        Assert.Throws<OverflowException>(() => inventory.AdjustStock(
            1,
            StockAdjustmentReason.StockCountCorrection,
            Guid.NewGuid()));

        Assert.Equal(int.MaxValue, inventory.OnHandQuantity);
        Assert.Empty(inventory.DomainEvents);
    }
}
