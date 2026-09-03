using OrderService.Domain.Aggregates;
using OrderService.Domain.Enums;

namespace OrderService.UnitTests;

public sealed class OrderInventoryDomainTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static OrderAggregate Pending()
    {
        var order = OrderAggregate.Create(Guid.NewGuid(), Guid.NewGuid());
        order.AddItem(Guid.NewGuid(), "Product", 10, 2);
        order.BeginInventoryReservation(Guid.NewGuid(), Now, TimeSpan.FromMinutes(15));
        return order;
    }

    [Fact]
    public void StartReservation_PersistsIdentityDeadlineAndFreezesLines()
    {
        var order = Pending();
        Assert.NotEqual(Guid.Empty, order.InventoryReservationRequestId);
        Assert.Equal(Now.AddMinutes(15), order.InventoryReservationExpiresAtUtc);
        Assert.Equal(OrderInventoryStatus.Requested, order.InventoryStatus);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Throws<InvalidOperationException>(() => order.AddItem(Guid.NewGuid(), "Another", 1, 1));
        Assert.Throws<InvalidOperationException>(() => order.BeginInventoryReservation(Guid.NewGuid(), Now, TimeSpan.FromMinutes(1)));
    }

    [Theory]
    [InlineData(OrderInventoryStatus.Reserved, 1, OrderStatus.InventoryReserved)]
    [InlineData(OrderInventoryStatus.Rejected, 1, OrderStatus.Failed)]
    [InlineData(OrderInventoryStatus.Released, 1, OrderStatus.Failed)]
    [InlineData(OrderInventoryStatus.Expired, 2, OrderStatus.Failed)]
    [InlineData(OrderInventoryStatus.Committed, 2, OrderStatus.RequiresAttention)]
    public void Results_DriveOnlyInventoryLifecycle(OrderInventoryStatus result, long version, OrderStatus expected)
    {
        var order = Pending();
        Assert.True(order.ApplyInventoryResult(order.InventoryReservationRequestId!.Value, version, result, "Reason"));
        Assert.Equal(expected, order.Status);
        Assert.Equal(result, order.InventoryStatus);
        Assert.Equal(version, order.InventoryReservationVersion);
        Assert.NotEqual(OrderStatus.Confirmed, order.Status);
        Assert.Equal(result == OrderInventoryStatus.Reserved ? null : "Reason", order.InventoryFailureReason);
    }

    [Theory]
    [InlineData(OrderInventoryStatus.Rejected)]
    [InlineData(OrderInventoryStatus.Released)]
    [InlineData(OrderInventoryStatus.Expired)]
    [InlineData(OrderInventoryStatus.Committed)]
    public void TerminalResultCannotBeResurrectedByLateOrNewerReserved(OrderInventoryStatus result)
    {
        var order = Pending();
        var id = order.InventoryReservationRequestId!.Value;
        order.ApplyInventoryResult(id, 2, result);
        Assert.False(order.ApplyInventoryResult(id, 1, OrderInventoryStatus.Reserved));
        Assert.False(order.ApplyInventoryResult(id, 3, OrderInventoryStatus.Reserved));
        Assert.Equal(result, order.InventoryStatus);
        Assert.Equal(2, order.InventoryReservationVersion);
    }

    [Fact]
    public void DuplicatesAndOtherAttempts_AreNoOps()
    {
        var order = Pending();
        var id = order.InventoryReservationRequestId!.Value;
        Assert.False(order.ApplyInventoryResult(Guid.NewGuid(), 1, OrderInventoryStatus.Rejected));
        order.ApplyInventoryResult(id, 1, OrderInventoryStatus.Reserved);
        Assert.False(order.ApplyInventoryResult(id, 1, OrderInventoryStatus.Reserved));
        Assert.False(order.ApplyInventoryResult(id, 2, OrderInventoryStatus.Reserved));
        Assert.Equal(1, order.InventoryReservationVersion);
    }

    [Fact]
    public void ReservedOrderCanExpire()
    {
        var order = Pending();
        var id = order.InventoryReservationRequestId!.Value;
        order.ApplyInventoryResult(id, 1, OrderInventoryStatus.Reserved);
        Assert.True(order.ApplyInventoryResult(id, 2, OrderInventoryStatus.Expired, "DeadlineElapsed"));
        Assert.Equal(OrderStatus.Failed, order.Status);
    }

    [Theory]
    [InlineData(0, OrderInventoryStatus.Reserved)]
    [InlineData(-1, OrderInventoryStatus.Reserved)]
    [InlineData(1, OrderInventoryStatus.Requested)]
    [InlineData(1, (OrderInventoryStatus)999)]
    public void InvalidResultIsRejected(long version, OrderInventoryStatus result)
    {
        var order = Pending();
        Assert.Throws<ArgumentException>(() => order.ApplyInventoryResult(order.InventoryReservationRequestId!.Value, version, result));
    }

    [Fact]
    public void CommandRejectionNeedsAttentionButAuthoritativeResultCanResolveIt()
    {
        var order = Pending();
        var id = order.InventoryReservationRequestId!.Value;
        Assert.True(order.FlagInventoryCommandRejection(id, "Reserve", "RequestIdConflict"));
        Assert.Equal(OrderStatus.RequiresAttention, order.Status);
        Assert.False(order.FlagInventoryCommandRejection(id, "Reserve", "RequestIdConflict"));
        Assert.True(order.ApplyInventoryResult(id, 1, OrderInventoryStatus.Reserved));
        Assert.Equal(OrderStatus.InventoryReserved, order.Status);
        Assert.Null(order.InventoryFailureReason);
        Assert.False(order.FlagInventoryCommandRejection(id, "Reserve", "RequestIdConflict"));
    }

    [Theory]
    [InlineData("Commit")]
    [InlineData("Release")]
    public void UnissuedCommandRejectionDoesNotChangeOrder(string operation)
    {
        var order = Pending();
        Assert.False(order.FlagInventoryCommandRejection(order.InventoryReservationRequestId!.Value, operation, "Unknown"));
        Assert.False(order.FlagInventoryCommandRejection(Guid.NewGuid(), "Reserve", "Unknown"));
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1441)]
    public void InvalidDurationIsRejected(int minutes)
    {
        var order = OrderAggregate.Create(Guid.NewGuid(), Guid.NewGuid());
        order.AddItem(Guid.NewGuid(), "P", 1, 1);
        Assert.Throws<ArgumentException>(() => order.BeginInventoryReservation(Guid.NewGuid(), Now, TimeSpan.FromMinutes(minutes)));
    }

    [Fact]
    public void EmptyOrderOrRequestCannotStartReservation()
    {
        var order = OrderAggregate.Create(Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => order.BeginInventoryReservation(Guid.NewGuid(), Now, TimeSpan.FromMinutes(1)));
        order.AddItem(Guid.NewGuid(), "P", 1, 1);
        Assert.Throws<ArgumentException>(() => order.BeginInventoryReservation(Guid.Empty, Now, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void InvalidOrDuplicateProductsCannotEnterOrder()
    {
        var order = OrderAggregate.Create(Guid.NewGuid(), Guid.NewGuid());
        var product = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => order.AddItem(Guid.Empty, "P", 1, 1));
        Assert.Throws<ArgumentException>(() => order.AddItem(product, " ", 1, 1));
        Assert.Throws<ArgumentException>(() => order.AddItem(product, "P", 1.001m, 1));
        Assert.Throws<ArgumentException>(() => order.AddItem(product, "P", 10000000000000000m, 1));
        order.AddItem(product, "P", 1, 1);
        Assert.Throws<ArgumentException>(() => order.AddItem(product, "P", 1, 1));
    }
}
