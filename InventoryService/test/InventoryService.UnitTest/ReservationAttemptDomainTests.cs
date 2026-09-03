using Domain.Abstractions;
using InventoryService.Domain.Enums;
using InventoryService.Domain.Exceptions;

namespace InventoryService.UnitTest;

public sealed class ReservationAttemptDomainTests
{
    private static readonly DateTime Now = InventoryTestData.ReservedAtUtc;

    [Fact]
    public void RetryAfterRelease_UsesNewAttemptWithoutMutatingOldReservation()
    {
        var item = InventoryTestData.CreateInventory();
        var order = Guid.NewGuid();
        var firstId = Guid.NewGuid();
        var first = item.Reserve(order, firstId, 3, Now, Now.AddHours(1));
        item.ReleaseReservation(order, firstId, ReservationReleaseReason.OrderCancelled, Now.AddMinutes(1));
        var second = item.Reserve(order, Guid.NewGuid(), 5, Now.AddMinutes(2), Now.AddHours(1));
        Assert.Equal(StockReservationStatus.Released, first.Status);
        Assert.NotEqual(first.ReservationRequestId, second.ReservationRequestId);
        Assert.Equal(5, item.ReservedQuantity);
        item.ReleaseReservation(order, firstId, ReservationReleaseReason.OrderCancelled, Now.AddMinutes(3));
        Assert.Equal(5, item.ReservedQuantity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SecondAttempt_CannotDuplicateActiveOrCommittedOrder(bool commit)
    {
        var item = InventoryTestData.CreateInventory();
        var order = Guid.NewGuid();
        var request = Guid.NewGuid();
        item.Reserve(order, request, 2, Now, Now.AddHours(1));
        if (commit) item.CommitReservation(order, request, Now.AddMinutes(1));
        Assert.Throws<DomainException>(() => item.Reserve(order, Guid.NewGuid(), 2, Now, Now.AddHours(1)));
    }

    [Fact]
    public void EmptyAttempt_IsRejected()
        => Assert.Throws<DomainException>(() => InventoryTestData.CreateInventory()
            .Reserve(Guid.NewGuid(), Guid.Empty, 1, Now, Now.AddHours(1)));

    [Fact]
    public void AttemptCannotBeReusedByAnotherOrder()
    {
        var item = InventoryTestData.CreateInventory();
        var request = Guid.NewGuid();
        item.Reserve(Guid.NewGuid(), request, 2, Now, Now.AddHours(1));
        Assert.Throws<DomainException>(() => item.Reserve(Guid.NewGuid(), request, 2, Now, Now.AddHours(1)));
    }

    [Fact]
    public void CommitBeforeReservation_IsRejectedWithoutMutation()
    {
        var item = InventoryTestData.CreateInventory();
        var order = Guid.NewGuid();
        var request = Guid.NewGuid();
        var reservation = item.Reserve(order, request, 2, Now, Now.AddHours(1));
        Assert.Throws<InvalidStockReservationException>(() => item.CommitReservation(order, request, Now.AddTicks(-1)));
        Assert.Equal(StockReservationStatus.Reserved, reservation.Status);
        Assert.Equal(10, item.OnHandQuantity);
        Assert.Equal(2, item.ReservedQuantity);
    }

    [Fact]
    public void UndefinedAdjustmentReason_IsRejected()
        => Assert.Throws<DomainException>(() => InventoryTestData.CreateInventory()
            .AdjustStock(1, (StockAdjustmentReason)999, Guid.NewGuid()));

    [Fact]
    public void UndefinedReleaseReason_IsRejected()
    {
        var item = InventoryTestData.CreateInventory();
        var order = Guid.NewGuid();
        item.Reserve(order, 1, Now, Now.AddHours(1));
        Assert.Throws<InvalidStockReservationException>(() => item.ReleaseReservation(order,
            (ReservationReleaseReason)999, Now.AddMinutes(1)));
        Assert.Equal(1, item.ReservedQuantity);
    }
}
