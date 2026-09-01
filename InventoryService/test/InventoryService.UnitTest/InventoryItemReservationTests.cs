using Domain.Abstractions;
using InventoryService.Domain.DomainEvents;
using InventoryService.Domain.Enums;
using InventoryService.Domain.Exceptions;

namespace InventoryService.UnitTest;

public sealed class InventoryItemReservationTests
{
    [Fact]
    public void Reserve_WithAvailableStock_CreatesReservationAndUpdatesQuantities()
    {
        var inventory = InventoryTestData.CreateInventory(initialQuantity: 10);
        var orderId = Guid.NewGuid();

        var reservation = inventory.Reserve(
            orderId,
            4,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc);

        Assert.Equal(orderId, reservation.OrderId);
        Assert.Equal(4, reservation.Quantity);
        Assert.Equal(StockReservationStatus.Reserved, reservation.Status);
        Assert.Equal(InventoryTestData.ReservedAtUtc, reservation.ReservedAtUtc);
        Assert.Equal(InventoryTestData.ExpiresAtUtc, reservation.ExpiresAtUtc);
        Assert.True(reservation.IsActive);
        Assert.False(reservation.IsFinalized);
        Assert.Equal(10, inventory.OnHandQuantity);
        Assert.Equal(4, inventory.ReservedQuantity);
        Assert.Equal(6, inventory.AvailableQuantity);
        Assert.Single(inventory.Reservations);

        var domainEvent = Assert.IsType<StockReservedDomainEvent>(
            Assert.Single(inventory.DomainEvents));

        Assert.Equal(reservation.Id, domainEvent.ReservationId);
        Assert.Equal(orderId, domainEvent.OrderId);
        Assert.Equal(4, domainEvent.Quantity);
        Assert.Equal(6, domainEvent.AvailableQuantity);
    }

    [Fact]
    public void Reserve_WhenCrossingReorderPoint_RaisesLowStockEvent()
    {
        var inventory = InventoryTestData.CreateInventory(
            initialQuantity: 10,
            reorderPoint: 3);

        inventory.Reserve(
            Guid.NewGuid(),
            7,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc);

        Assert.IsType<StockReservedDomainEvent>(inventory.DomainEvents.First());
        var lowStockEvent = Assert.IsType<LowStockReachedDomainEvent>(
            inventory.DomainEvents.Last());
        Assert.Equal(3, lowStockEvent.AvailableQuantity);
    }

    [Fact]
    public void Reserve_WithInsufficientStock_ThrowsWithoutChangingState()
    {
        var inventory = InventoryTestData.CreateInventory(initialQuantity: 3);

        Assert.Throws<DomainException>(() => inventory.Reserve(
            Guid.NewGuid(),
            4,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc));

        Assert.Equal(3, inventory.OnHandQuantity);
        Assert.Equal(0, inventory.ReservedQuantity);
        Assert.Equal(3, inventory.AvailableQuantity);
        Assert.Empty(inventory.Reservations);
        Assert.Empty(inventory.DomainEvents);
    }

    [Fact]
    public void Reserve_DuplicateRequest_IsIdempotent()
    {
        var inventory = InventoryTestData.CreateInventory();
        var orderId = Guid.NewGuid();
        var first = inventory.Reserve(
            orderId,
            4,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc);
        inventory.ClearEvents();

        var second = inventory.Reserve(
            orderId,
            4,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc);

        Assert.Same(first, second);
        Assert.Equal(4, inventory.ReservedQuantity);
        Assert.Single(inventory.Reservations);
        Assert.Empty(inventory.DomainEvents);
    }

    [Fact]
    public void Reserve_DuplicateOrderWithDifferentQuantity_Throws()
    {
        var inventory = InventoryTestData.CreateInventory();
        var orderId = Guid.NewGuid();
        inventory.Reserve(
            orderId,
            4,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc);
        inventory.ClearEvents();

        Assert.Throws<DomainException>(() => inventory.Reserve(
            orderId,
            5,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc));

        Assert.Equal(4, inventory.ReservedQuantity);
        Assert.Empty(inventory.DomainEvents);
    }

    [Fact]
    public void Reserve_WithEmptyOrderId_ThrowsDomainException()
    {
        var inventory = InventoryTestData.CreateInventory();

        Assert.Throws<DomainException>(() => inventory.Reserve(
            Guid.Empty,
            1,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Reserve_WithNonPositiveQuantity_ThrowsDomainException(
        int quantity)
    {
        var inventory = InventoryTestData.CreateInventory();

        Assert.Throws<DomainException>(() => inventory.Reserve(
            Guid.NewGuid(),
            quantity,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc));
    }

    [Fact]
    public void Reserve_WithNonUtcTimestamp_ThrowsDomainException()
    {
        var inventory = InventoryTestData.CreateInventory();
        var localTime = DateTime.SpecifyKind(
            InventoryTestData.ReservedAtUtc,
            DateTimeKind.Local);

        Assert.Throws<DomainException>(() => inventory.Reserve(
            Guid.NewGuid(),
            1,
            localTime,
            InventoryTestData.ExpiresAtUtc));
    }

    [Fact]
    public void Reserve_WithInvalidExpiration_ThrowsReservationException()
    {
        var inventory = InventoryTestData.CreateInventory();

        Assert.Throws<InvalidStockReservationException>(() => inventory.Reserve(
            Guid.NewGuid(),
            1,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ReservedAtUtc));
    }

    [Fact]
    public void CommitReservation_UpdatesStockAndFinalizesReservation()
    {
        var inventory = InventoryTestData.CreateInventory(initialQuantity: 10);
        var orderId = Guid.NewGuid();
        var reservation = inventory.Reserve(
            orderId,
            4,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc);
        inventory.ClearEvents();
        var committedAt = InventoryTestData.ReservedAtUtc.AddMinutes(10);

        inventory.CommitReservation(orderId, committedAt);

        Assert.Equal(StockReservationStatus.Committed, reservation.Status);
        Assert.True(reservation.IsFinalized);
        Assert.False(reservation.IsActive);
        Assert.Equal(committedAt, reservation.FinalizedAtUtc);
        Assert.Null(reservation.ReleaseReason);
        Assert.Equal(6, inventory.OnHandQuantity);
        Assert.Equal(0, inventory.ReservedQuantity);
        Assert.Equal(6, inventory.AvailableQuantity);

        var domainEvent = Assert.IsType<StockReservationCommittedDomainEvent>(
            Assert.Single(inventory.DomainEvents));
        Assert.Equal(4, domainEvent.Quantity);
        Assert.Equal(6, domainEvent.OnHandQuantity);
    }

    [Fact]
    public void CommitReservation_DuplicateRequest_IsIdempotent()
    {
        var inventory = InventoryTestData.CreateInventory();
        var orderId = Guid.NewGuid();
        inventory.Reserve(
            orderId,
            4,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc);
        var committedAt = InventoryTestData.ReservedAtUtc.AddMinutes(10);
        inventory.CommitReservation(orderId, committedAt);
        inventory.ClearEvents();

        inventory.CommitReservation(orderId, committedAt);

        Assert.Equal(6, inventory.OnHandQuantity);
        Assert.Equal(0, inventory.ReservedQuantity);
        Assert.Empty(inventory.DomainEvents);
    }

    [Fact]
    public void CommitReservation_AfterExpiration_ThrowsWithoutChangingStock()
    {
        var inventory = InventoryTestData.CreateInventory();
        var orderId = Guid.NewGuid();
        var reservation = inventory.Reserve(
            orderId,
            4,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc);
        inventory.ClearEvents();

        Assert.Throws<StockReservationExpiredException>(() =>
            inventory.CommitReservation(orderId, InventoryTestData.ExpiresAtUtc));

        Assert.Equal(StockReservationStatus.Reserved, reservation.Status);
        Assert.Equal(10, inventory.OnHandQuantity);
        Assert.Equal(4, inventory.ReservedQuantity);
        Assert.Empty(inventory.DomainEvents);
    }

    [Fact]
    public void ReleaseReservation_RestoresAvailableQuantityAndFinalizesReservation()
    {
        var inventory = InventoryTestData.CreateInventory();
        var orderId = Guid.NewGuid();
        var reservation = inventory.Reserve(
            orderId,
            4,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc);
        inventory.ClearEvents();
        var releasedAt = InventoryTestData.ReservedAtUtc.AddMinutes(5);

        inventory.ReleaseReservation(
            orderId,
            ReservationReleaseReason.PaymentFailed,
            releasedAt);

        Assert.Equal(StockReservationStatus.Released, reservation.Status);
        Assert.Equal(ReservationReleaseReason.PaymentFailed, reservation.ReleaseReason);
        Assert.Equal(releasedAt, reservation.FinalizedAtUtc);
        Assert.Equal(10, inventory.OnHandQuantity);
        Assert.Equal(0, inventory.ReservedQuantity);
        Assert.Equal(10, inventory.AvailableQuantity);

        Assert.IsType<StockReservationReleasedDomainEvent>(
            Assert.Single(inventory.DomainEvents));
    }

    [Fact]
    public void ReleaseReservation_DuplicateRequest_IsIdempotent()
    {
        var inventory = InventoryTestData.CreateInventory();
        var orderId = Guid.NewGuid();
        inventory.Reserve(
            orderId,
            4,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc);
        var releasedAt = InventoryTestData.ReservedAtUtc.AddMinutes(5);
        inventory.ReleaseReservation(
            orderId,
            ReservationReleaseReason.OrderCancelled,
            releasedAt);
        inventory.ClearEvents();

        inventory.ReleaseReservation(
            orderId,
            ReservationReleaseReason.OrderCancelled,
            releasedAt);

        Assert.Equal(0, inventory.ReservedQuantity);
        Assert.Equal(10, inventory.AvailableQuantity);
        Assert.Empty(inventory.DomainEvents);
    }

    [Theory]
    [InlineData(ReservationReleaseReason.None)]
    [InlineData(ReservationReleaseReason.Expired)]
    public void ReleaseReservation_WithInvalidReason_Throws(
        ReservationReleaseReason reason)
    {
        var inventory = InventoryTestData.CreateInventory();
        var orderId = Guid.NewGuid();
        inventory.Reserve(
            orderId,
            2,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc);
        inventory.ClearEvents();

        Assert.Throws<InvalidStockReservationException>(() =>
            inventory.ReleaseReservation(
                orderId,
                reason,
                InventoryTestData.ReservedAtUtc.AddMinutes(1)));

        Assert.Equal(2, inventory.ReservedQuantity);
        Assert.Empty(inventory.DomainEvents);
    }

    [Fact]
    public void ReleaseReservation_AfterCommit_ThrowsInvalidState()
    {
        var inventory = InventoryTestData.CreateInventory();
        var orderId = Guid.NewGuid();
        inventory.Reserve(
            orderId,
            2,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc);
        inventory.CommitReservation(
            orderId,
            InventoryTestData.ReservedAtUtc.AddMinutes(1));
        inventory.ClearEvents();

        Assert.Throws<InvalidReservationStateException>(() =>
            inventory.ReleaseReservation(
                orderId,
                ReservationReleaseReason.OrderCancelled,
                InventoryTestData.ReservedAtUtc.AddMinutes(2)));

        Assert.Equal(8, inventory.OnHandQuantity);
        Assert.Equal(0, inventory.ReservedQuantity);
    }

    [Fact]
    public void ExpireReservation_AfterExpiration_ReleasesStock()
    {
        var inventory = InventoryTestData.CreateInventory();
        var orderId = Guid.NewGuid();
        var reservation = inventory.Reserve(
            orderId,
            3,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc);
        inventory.ClearEvents();

        inventory.ExpireReservation(orderId, InventoryTestData.ExpiresAtUtc);

        Assert.Equal(StockReservationStatus.Expired, reservation.Status);
        Assert.Equal(ReservationReleaseReason.Expired, reservation.ReleaseReason);
        Assert.Equal(0, inventory.ReservedQuantity);
        Assert.Equal(10, inventory.AvailableQuantity);
        Assert.IsType<StockReservationExpiredDomainEvent>(
            Assert.Single(inventory.DomainEvents));
    }

    [Fact]
    public void ExpireReservation_BeforeExpiration_ThrowsInvalidState()
    {
        var inventory = InventoryTestData.CreateInventory();
        var orderId = Guid.NewGuid();
        inventory.Reserve(
            orderId,
            3,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc);
        inventory.ClearEvents();

        Assert.Throws<InvalidReservationStateException>(() =>
            inventory.ExpireReservation(
                orderId,
                InventoryTestData.ExpiresAtUtc.AddTicks(-1)));

        Assert.Equal(3, inventory.ReservedQuantity);
        Assert.Empty(inventory.DomainEvents);
    }

    [Fact]
    public void ExpireReservation_DuplicateRequest_IsIdempotent()
    {
        var inventory = InventoryTestData.CreateInventory();
        var orderId = Guid.NewGuid();
        inventory.Reserve(
            orderId,
            3,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc);
        inventory.ExpireReservation(orderId, InventoryTestData.ExpiresAtUtc);
        inventory.ClearEvents();

        inventory.ExpireReservation(orderId, InventoryTestData.ExpiresAtUtc);

        Assert.Equal(0, inventory.ReservedQuantity);
        Assert.Empty(inventory.DomainEvents);
    }

    [Fact]
    public void ReservationOperation_ForUnknownOrder_ThrowsDomainException()
    {
        var inventory = InventoryTestData.CreateInventory();

        var exception = Assert.Throws<DomainException>(() =>
            inventory.CommitReservation(
                Guid.NewGuid(),
                InventoryTestData.ReservedAtUtc));

        Assert.Contains("was not found", exception.Message);
    }
}
