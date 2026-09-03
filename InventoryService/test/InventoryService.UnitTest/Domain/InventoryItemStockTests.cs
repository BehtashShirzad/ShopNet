using Domain.Abstractions;
using InventoryService.Domain.DomainEvents;
using InventoryService.Domain.Enums;

namespace InventoryService.UnitTest;

public sealed class InventoryItemStockTests
{
    [Fact]
    public void ReceiveStock_IncreasesOnHandAndAvailableQuantity()
    {
        var inventory = InventoryTestData.CreateInventory(initialQuantity: 10);
        var referenceId = Guid.NewGuid();

        inventory.ReceiveStock(5, referenceId);

        Assert.Equal(15, inventory.OnHandQuantity);
        Assert.Equal(0, inventory.ReservedQuantity);
        Assert.Equal(15, inventory.AvailableQuantity);

        var domainEvent = Assert.IsType<StockReceivedDomainEvent>(
            Assert.Single(inventory.DomainEvents));

        Assert.Equal(5, domainEvent.Quantity);
        Assert.Equal(referenceId, domainEvent.ReferenceId);
        Assert.Equal(15, domainEvent.OnHandQuantity);
        Assert.Equal(15, domainEvent.AvailableQuantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ReceiveStock_WithNonPositiveQuantity_ThrowsDomainException(
        int quantity)
    {
        var inventory = InventoryTestData.CreateInventory();

        Assert.Throws<DomainException>(
            () => inventory.ReceiveStock(quantity, Guid.NewGuid()));

        Assert.Equal(10, inventory.OnHandQuantity);
        Assert.Empty(inventory.DomainEvents);
    }

    [Fact]
    public void ReceiveStock_WithEmptyReferenceId_ThrowsDomainException()
    {
        var inventory = InventoryTestData.CreateInventory();

        Assert.Throws<DomainException>(
            () => inventory.ReceiveStock(1, Guid.Empty));

        Assert.Equal(10, inventory.OnHandQuantity);
    }

    [Fact]
    public void AdjustStock_WithValidReduction_UpdatesQuantitiesAndRaisesEvent()
    {
        var inventory = InventoryTestData.CreateInventory(initialQuantity: 10);
        var referenceId = Guid.NewGuid();

        inventory.AdjustStock(
            -3,
            StockAdjustmentReason.Damaged,
            referenceId);

        Assert.Equal(7, inventory.OnHandQuantity);
        Assert.Equal(7, inventory.AvailableQuantity);

        var domainEvent = Assert.IsType<StockAdjustedDomainEvent>(
            Assert.Single(inventory.DomainEvents));

        Assert.Equal(-3, domainEvent.QuantityDelta);
        Assert.Equal(StockAdjustmentReason.Damaged, domainEvent.Reason);
        Assert.Equal(referenceId, domainEvent.ReferenceId);
        Assert.Equal(7, domainEvent.OnHandQuantity);
    }

    [Fact]
    public void AdjustStock_WithPositiveCorrection_IncreasesStock()
    {
        var inventory = InventoryTestData.CreateInventory(initialQuantity: 10);

        inventory.AdjustStock(
            4,
            StockAdjustmentReason.StockCountCorrection,
            Guid.NewGuid());

        Assert.Equal(14, inventory.OnHandQuantity);
        Assert.Equal(14, inventory.AvailableQuantity);
    }

    [Fact]
    public void AdjustStock_ToNegativeOnHand_ThrowsWithoutChangingState()
    {
        var inventory = InventoryTestData.CreateInventory(initialQuantity: 10);

        Assert.Throws<DomainException>(() => inventory.AdjustStock(
            -11,
            StockAdjustmentReason.Damaged,
            Guid.NewGuid()));

        Assert.Equal(10, inventory.OnHandQuantity);
        Assert.Empty(inventory.DomainEvents);
    }

    [Fact]
    public void AdjustStock_BelowReservedQuantity_ThrowsWithoutChangingState()
    {
        var inventory = InventoryTestData.CreateInventory(initialQuantity: 10);
        inventory.Reserve(
            Guid.NewGuid(),
            8,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc);
        inventory.ClearEvents();

        Assert.Throws<DomainException>(() => inventory.AdjustStock(
            -3,
            StockAdjustmentReason.StockCountCorrection,
            Guid.NewGuid()));

        Assert.Equal(10, inventory.OnHandQuantity);
        Assert.Equal(8, inventory.ReservedQuantity);
        Assert.Equal(2, inventory.AvailableQuantity);
        Assert.Empty(inventory.DomainEvents);
    }

    [Fact]
    public void AdjustStock_WhenCrossingReorderPoint_RaisesLowStockEvent()
    {
        var inventory = InventoryTestData.CreateInventory(
            initialQuantity: 10,
            reorderPoint: 3);

        inventory.AdjustStock(
            -7,
            StockAdjustmentReason.Damaged,
            Guid.NewGuid());

        var lowStockEvent = Assert.IsType<LowStockReachedDomainEvent>(
            inventory.DomainEvents.Last());

        Assert.Equal(3, lowStockEvent.AvailableQuantity);
        Assert.Equal(3, lowStockEvent.ReorderPoint);
        Assert.Equal(2, inventory.DomainEvents.Count);
    }

    [Fact]
    public void AdjustStock_WithZeroDelta_ThrowsDomainException()
    {
        var inventory = InventoryTestData.CreateInventory();

        Assert.Throws<DomainException>(() => inventory.AdjustStock(
            0,
            StockAdjustmentReason.StockCountCorrection,
            Guid.NewGuid()));
    }

    [Fact]
    public void AdjustStock_WithoutReason_ThrowsDomainException()
    {
        var inventory = InventoryTestData.CreateInventory();

        Assert.Throws<DomainException>(() => inventory.AdjustStock(
            -1,
            StockAdjustmentReason.None,
            Guid.NewGuid()));
    }
}
