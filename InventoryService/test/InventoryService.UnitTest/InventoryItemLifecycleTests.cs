using Domain.Abstractions;
using InventoryService.Domain.DomainEvents;
using InventoryService.Domain.Enums;

namespace InventoryService.UnitTest;

public sealed class InventoryItemLifecycleTests
{
    [Fact]
    public void Deactivate_WithoutActiveReservations_DeactivatesInventory()
    {
        var inventory = InventoryTestData.CreateInventory();

        inventory.Deactivate();

        Assert.False(inventory.IsActive);
        Assert.IsType<InventoryItemDeactivatedDomainEvent>(
            Assert.Single(inventory.DomainEvents));
    }

    [Fact]
    public void Deactivate_WithActiveReservation_ThrowsDomainException()
    {
        var inventory = InventoryTestData.CreateInventory();
        inventory.Reserve(
            Guid.NewGuid(),
            1,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc);
        inventory.ClearEvents();

        Assert.Throws<DomainException>(inventory.Deactivate);

        Assert.True(inventory.IsActive);
        Assert.Empty(inventory.DomainEvents);
    }

    [Fact]
    public void Deactivate_AfterReservationReleased_Succeeds()
    {
        var inventory = InventoryTestData.CreateInventory();
        var orderId = Guid.NewGuid();
        inventory.Reserve(
            orderId,
            1,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc);
        inventory.ReleaseReservation(
            orderId,
            ReservationReleaseReason.OrderCancelled,
            InventoryTestData.ReservedAtUtc.AddMinutes(1));
        inventory.ClearEvents();

        inventory.Deactivate();

        Assert.False(inventory.IsActive);
    }

    [Fact]
    public void Activate_AfterDeactivation_ActivatesAndRaisesEvent()
    {
        var inventory = InventoryTestData.CreateInventory();
        inventory.Deactivate();
        inventory.ClearEvents();

        inventory.Activate();

        Assert.True(inventory.IsActive);
        Assert.IsType<InventoryItemActivatedDomainEvent>(
            Assert.Single(inventory.DomainEvents));
    }

    [Fact]
    public void Activate_WhenAlreadyActive_IsIdempotent()
    {
        var inventory = InventoryTestData.CreateInventory();

        inventory.Activate();

        Assert.True(inventory.IsActive);
        Assert.Empty(inventory.DomainEvents);
    }

    [Fact]
    public void InactiveInventory_RejectsStockAndReservationOperations()
    {
        var inventory = InventoryTestData.CreateInventory();
        inventory.Deactivate();
        inventory.ClearEvents();

        Assert.Throws<DomainException>(
            () => inventory.ReceiveStock(1, Guid.NewGuid()));
        Assert.Throws<DomainException>(() => inventory.AdjustStock(
            1,
            StockAdjustmentReason.StockCountCorrection,
            Guid.NewGuid()));
        Assert.Throws<DomainException>(() => inventory.Reserve(
            Guid.NewGuid(),
            1,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc));

        Assert.Equal(10, inventory.OnHandQuantity);
        Assert.Empty(inventory.Reservations);
        Assert.Empty(inventory.DomainEvents);
    }

    [Fact]
    public void ChangeReorderPoint_UpdatesValueAndRaisesEvent()
    {
        var inventory = InventoryTestData.CreateInventory(reorderPoint: 2);

        inventory.ChangeReorderPoint(5);

        Assert.Equal(5, inventory.ReorderPoint);
        var domainEvent = Assert.IsType<ReorderPointChangedDomainEvent>(
            Assert.Single(inventory.DomainEvents));
        Assert.Equal(2, domainEvent.PreviousReorderPoint);
        Assert.Equal(5, domainEvent.NewReorderPoint);
    }

    [Fact]
    public void ChangeReorderPoint_ToSameValue_IsIdempotent()
    {
        var inventory = InventoryTestData.CreateInventory(reorderPoint: 2);

        inventory.ChangeReorderPoint(2);

        Assert.Empty(inventory.DomainEvents);
    }

    [Fact]
    public void ChangeReorderPoint_ToNegativeValue_ThrowsDomainException()
    {
        var inventory = InventoryTestData.CreateInventory();

        Assert.Throws<DomainException>(
            () => inventory.ChangeReorderPoint(-1));

        Assert.Equal(2, inventory.ReorderPoint);
        Assert.Empty(inventory.DomainEvents);
    }
}
