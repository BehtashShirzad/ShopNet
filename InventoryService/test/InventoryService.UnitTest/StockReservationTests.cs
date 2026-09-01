using InventoryService.Domain.Entities;
using InventoryService.Domain.Enums;
using InventoryService.Domain.Exceptions;

namespace InventoryService.UnitTest;

public sealed class StockReservationTests
{
    [Fact]
    public void Create_WithValidValues_InitializesReservedState()
    {
        var orderId = Guid.NewGuid();

        var reservation = StockReservation.Create(
            orderId,
            3,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc);

        Assert.NotEqual(Guid.Empty, reservation.Id);
        Assert.Equal(orderId, reservation.OrderId);
        Assert.Equal(3, reservation.Quantity);
        Assert.Equal(StockReservationStatus.Reserved, reservation.Status);
        Assert.Equal(InventoryTestData.ReservedAtUtc, reservation.ReservedAtUtc);
        Assert.Equal(InventoryTestData.ExpiresAtUtc, reservation.ExpiresAtUtc);
        Assert.Equal(InventoryTestData.ReservedAtUtc, reservation.CreatedAt);
        Assert.True(reservation.IsActive);
        Assert.False(reservation.IsFinalized);
        Assert.Null(reservation.FinalizedAtUtc);
        Assert.Null(reservation.ReleaseReason);
    }

    [Fact]
    public void Create_WithEmptyOrderId_Throws()
    {
        Assert.Throws<InvalidStockReservationException>(() =>
            StockReservation.Create(
                Guid.Empty,
                1,
                InventoryTestData.ReservedAtUtc,
                InventoryTestData.ExpiresAtUtc));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositiveQuantity_Throws(int quantity)
    {
        Assert.Throws<InvalidStockReservationException>(() =>
            StockReservation.Create(
                Guid.NewGuid(),
                quantity,
                InventoryTestData.ReservedAtUtc,
                InventoryTestData.ExpiresAtUtc));
    }

    [Fact]
    public void Create_WithNonUtcReservedAt_Throws()
    {
        var localTime = DateTime.SpecifyKind(
            InventoryTestData.ReservedAtUtc,
            DateTimeKind.Local);

        Assert.Throws<InvalidStockReservationException>(() =>
            StockReservation.Create(
                Guid.NewGuid(),
                1,
                localTime,
                InventoryTestData.ExpiresAtUtc));
    }

    [Fact]
    public void Create_WithNonUtcExpiresAt_Throws()
    {
        var unspecifiedTime = DateTime.SpecifyKind(
            InventoryTestData.ExpiresAtUtc,
            DateTimeKind.Unspecified);

        Assert.Throws<InvalidStockReservationException>(() =>
            StockReservation.Create(
                Guid.NewGuid(),
                1,
                InventoryTestData.ReservedAtUtc,
                unspecifiedTime));
    }

    [Fact]
    public void IsExpiredAt_ReflectsTimeAndState()
    {
        var reservation = CreateReservation();

        Assert.False(reservation.IsExpiredAt(
            InventoryTestData.ExpiresAtUtc.AddTicks(-1)));
        Assert.True(reservation.IsExpiredAt(
            InventoryTestData.ExpiresAtUtc));

        reservation.Expire(InventoryTestData.ExpiresAtUtc);

        Assert.False(reservation.IsExpiredAt(
            InventoryTestData.ExpiresAtUtc.AddMinutes(1)));
    }

    [Fact]
    public void IsExpiredAt_WithNonUtcTime_Throws()
    {
        var reservation = CreateReservation();
        var localTime = DateTime.SpecifyKind(
            InventoryTestData.ExpiresAtUtc,
            DateTimeKind.Local);

        Assert.Throws<InvalidStockReservationException>(
            () => reservation.IsExpiredAt(localTime));
    }

    [Fact]
    public void Matches_RequiresSameOrderAndQuantity()
    {
        var orderId = Guid.NewGuid();
        var reservation = StockReservation.Create(
            orderId,
            3,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc);

        Assert.True(reservation.Matches(orderId, 3));
        Assert.False(reservation.Matches(Guid.NewGuid(), 3));
        Assert.False(reservation.Matches(orderId, 4));
    }

    [Fact]
    public void Commit_TransitionsToCommittedAndIsIdempotent()
    {
        var reservation = CreateReservation();
        var committedAt = InventoryTestData.ReservedAtUtc.AddMinutes(1);

        Assert.True(reservation.Commit(committedAt));
        Assert.False(reservation.Commit(committedAt));
        Assert.Equal(StockReservationStatus.Committed, reservation.Status);
        Assert.Equal(committedAt, reservation.FinalizedAtUtc);
        Assert.True(reservation.IsFinalized);
    }

    [Fact]
    public void Commit_AtExpiration_ThrowsExpiredException()
    {
        var reservation = CreateReservation();

        var exception = Assert.Throws<StockReservationExpiredException>(
            () => reservation.Commit(InventoryTestData.ExpiresAtUtc));

        Assert.Equal(reservation.Id, exception.ReservationId);
        Assert.Equal(reservation.OrderId, exception.OrderId);
        Assert.Equal(InventoryTestData.ExpiresAtUtc, exception.ExpiresAtUtc);
        Assert.Equal(StockReservationStatus.Reserved, reservation.Status);
    }

    [Fact]
    public void Commit_WithNonUtcTime_Throws()
    {
        var reservation = CreateReservation();
        var localTime = DateTime.SpecifyKind(
            InventoryTestData.ReservedAtUtc.AddMinutes(1),
            DateTimeKind.Local);

        Assert.Throws<InvalidStockReservationException>(
            () => reservation.Commit(localTime));
    }

    [Fact]
    public void Release_BeforeReservationTime_ThrowsWithoutChangingState()
    {
        var reservation = CreateReservation();

        Assert.Throws<InvalidStockReservationException>(() =>
            reservation.Release(
                ReservationReleaseReason.OrderCancelled,
                InventoryTestData.ReservedAtUtc.AddTicks(-1)));

        Assert.Equal(StockReservationStatus.Reserved, reservation.Status);
    }

    [Fact]
    public void Release_WithDifferentReasonAfterRelease_ThrowsInvalidState()
    {
        var reservation = CreateReservation();
        var releasedAt = InventoryTestData.ReservedAtUtc.AddMinutes(1);
        reservation.Release(
            ReservationReleaseReason.OrderCancelled,
            releasedAt);

        var exception = Assert.Throws<InvalidReservationStateException>(() =>
            reservation.Release(
                ReservationReleaseReason.PaymentFailed,
                releasedAt));

        Assert.Equal(reservation.Id, exception.ReservationId);
        Assert.Equal(StockReservationStatus.Released, exception.CurrentStatus);
    }

    [Fact]
    public void Expire_AfterCommit_ThrowsInvalidState()
    {
        var reservation = CreateReservation();
        reservation.Commit(InventoryTestData.ReservedAtUtc.AddMinutes(1));

        Assert.Throws<InvalidReservationStateException>(
            () => reservation.Expire(InventoryTestData.ExpiresAtUtc));
    }

    private static StockReservation CreateReservation()
    {
        return StockReservation.Create(
            Guid.NewGuid(),
            3,
            InventoryTestData.ReservedAtUtc,
            InventoryTestData.ExpiresAtUtc);
    }
}
