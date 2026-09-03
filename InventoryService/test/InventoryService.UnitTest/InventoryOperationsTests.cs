using InventoryService.Application;
using InventoryService.Domain.Aggregates;
using ShopNet.Contracts;
using ShopNet.Contracts.Inventory.V1;

namespace InventoryService.UnitTest;

public sealed class InventoryOperationsTests
{
    private readonly MemoryStore _store = new();
    private readonly EventRecorder _events = new();
    private readonly TestClock _clock = new();
    private InventoryOperations Operations => new(_store, _events, _clock);
    private Guid Product(int stock = 10)
    {
        var item = InventoryItem.Create(Guid.NewGuid(), stock);
        _store.Add(item);
        return item.ProductId;
    }
    private ReserveInventory Request(params InventoryLine[] items)
        => new(Guid.NewGuid(), Guid.NewGuid(), items, _clock.GetUtcNow().AddMinutes(30));

    [Fact]
    public async Task RegisterProduct_IsIdempotentAndStartsWithZeroStock()
    {
        var id = Guid.NewGuid();
        await Operations.RegisterProductAsync(id);
        await Operations.RegisterProductAsync(id);
        Assert.Equal(0, Assert.Single(_store.Products.Values).OnHandQuantity);
        Assert.Empty(_events.Messages);
    }

    [Fact]
    public async Task Receipt_IsIdempotentAndRejectsDifferentPayload()
    {
        var product = Product();
        var command = new ReceiveInventoryStock(product, 5, Guid.NewGuid());
        await Operations.ReceiveStockAsync(command);
        await Operations.ReceiveStockAsync(command);
        Assert.Equal(15, _store.Products[product].OnHandQuantity);
        await Assert.ThrowsAsync<ArgumentException>(() => Operations.ReceiveStockAsync(command with { Quantity = 6 }));
        Assert.Single(_store.Receipts);
    }

    [Fact]
    public async Task ReserveAllLines_PublishesCorrelatedResult()
    {
        var p1 = Product();
        var p2 = Product();
        var request = Request(new InventoryLine(p1, 2), new(p2, 3));
        await Operations.ReserveAsync(request);
        var result = Assert.IsType<InventoryReserved>(Assert.Single(_events.Messages));
        Assert.Equal(request.OrderId, result.OrderId);
        Assert.Equal(request.ReservationRequestId, result.ReservationRequestId);
        Assert.Equal(1, result.ReservationVersion);
        Assert.Equal(2, result.Items.Length);
        Assert.Equal(2, _store.Products[p1].ReservedQuantity);
        Assert.Equal(3, _store.Products[p2].ReservedQuantity);
    }

    [Fact]
    public async Task InsufficientLine_RejectsEntireBatchAndPersistsRejection()
    {
        var available = Product();
        var empty = Product(0);
        var request = Request(new InventoryLine(available, 1), new(empty, 1));
        await Operations.ReserveAsync(request);
        var result = Assert.IsType<InventoryRejected>(Assert.Single(_events.Messages));
        Assert.Equal("InsufficientStock", result.Reason);
        Assert.All(_store.Products.Values, x => Assert.Equal(0, x.ReservedQuantity));
        _store.Products[empty].ReceiveStock(10, Guid.NewGuid());
        await Operations.ReserveAsync(request);
        Assert.Equal(result.EventId, _events.Messages.Last().EventId);
        Assert.All(_store.Products.Values, x => Assert.Equal(0, x.ReservedQuantity));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("inactive")]
    [InlineData("elapsed")]
    [InlineData("tooFar")]
    public async Task InvalidBusinessCondition_ProducesRejection(string condition)
    {
        var p = Product();
        var request = Request(new InventoryLine(p, 1));
        if (condition == "missing") request = request with { Items = [new(Guid.NewGuid(), 1)] };
        if (condition == "inactive") _store.Products[p].Deactivate();
        if (condition == "elapsed") request = request with { ExpiresAtUtc = _clock.GetUtcNow() };
        if (condition == "tooFar") request = request with { ExpiresAtUtc = _clock.GetUtcNow().AddDays(2) };
        await Operations.ReserveAsync(request);
        Assert.IsType<InventoryRejected>(Assert.Single(_events.Messages));
        Assert.Equal(0, _store.Products[p].ReservedQuantity);
    }

    [Fact]
    public async Task Retry_ReorderedLinesReplaysSameEventIdentity()
    {
        var p1 = Product();
        var p2 = Product();
        var request = Request(new InventoryLine(p1, 2), new(p2, 3));
        await Operations.ReserveAsync(request);
        await Operations.ReserveAsync(request with { Items = request.Items.Reverse().ToArray() });
        Assert.Equal(_events.Messages[0].EventId, _events.Messages[1].EventId);
        Assert.Equal(_events.Messages[0].OccurredOnUtc, _events.Messages[1].OccurredOnUtc);
        Assert.Equal(2, _store.Products[p1].ReservedQuantity);
    }

    [Theory]
    [InlineData("quantity")]
    [InlineData("expiry")]
    [InlineData("order")]
    public async Task ConflictingRequestId_DoesNotAlterReservation(string conflict)
    {
        var p = Product();
        var request = Request(new InventoryLine(p, 2));
        await Operations.ReserveAsync(request);
        var changed = conflict switch
        {
            "quantity" => request with { Items = [new(p, 3)] },
            "expiry" => request with { ExpiresAtUtc = request.ExpiresAtUtc.AddMinutes(1) },
            _ => request with { OrderId = Guid.NewGuid() }
        };
        await Operations.ReserveAsync(changed);
        Assert.IsType<InventoryCommandRejected>(_events.Messages.Last());
        Assert.Equal(2, _store.Products[p].ReservedQuantity);
    }

    [Fact]
    public async Task Commit_IsIdempotentAndReleaseCannotUndoIt()
    {
        var p = Product();
        var request = Request(new InventoryLine(p, 3));
        await Operations.ReserveAsync(request);
        var command = new CommitInventory(request.OrderId, request.ReservationRequestId);
        await Operations.CommitAsync(command);
        var committed = Assert.IsType<InventoryCommitted>(_events.Messages.Last());
        Assert.Equal(2, committed.ReservationVersion);
        await Operations.CommitAsync(command);
        await Operations.ReleaseAsync(new(request.OrderId, request.ReservationRequestId, InventoryReleaseReason.Compensation));
        Assert.Equal(committed.EventId, _events.Messages.Last().EventId);
        Assert.Equal(2, Assert.IsType<InventoryCommitted>(_events.Messages.Last()).ReservationVersion);
        Assert.Equal(7, _store.Products[p].OnHandQuantity);
        Assert.Equal(0, _store.Products[p].ReservedQuantity);
    }

    [Fact]
    public async Task ReleaseBeforeReserve_FencesLateRequest()
    {
        var p = Product();
        var request = Request(new InventoryLine(p, 3));
        await Operations.ReleaseAsync(new(request.OrderId, request.ReservationRequestId, InventoryReleaseReason.OrderCancelled));
        await Operations.ReserveAsync(request);
        Assert.All(_events.Messages, x => Assert.IsType<InventoryReleased>(x));
        Assert.Equal(_events.Messages[0].EventId, _events.Messages[1].EventId);
        Assert.Equal(0, _store.Products[p].ReservedQuantity);
    }

    [Fact]
    public async Task ExpiredCommit_ReleasesWholeBatchAndCannotReviveOnRetry()
    {
        var p = Product();
        var request = Request(new InventoryLine(p, 3));
        await Operations.ReserveAsync(request);
        _clock.Now = request.ExpiresAtUtc;
        await Operations.CommitAsync(new(request.OrderId, request.ReservationRequestId));
        var expired = Assert.IsType<InventoryExpired>(_events.Messages.Last());
        await Operations.ReserveAsync(request);
        Assert.Equal(expired.EventId, _events.Messages.Last().EventId);
        Assert.Equal(10, _store.Products[p].OnHandQuantity);
        Assert.Equal(0, _store.Products[p].ReservedQuantity);
    }

    [Fact]
    public async Task ExpiryBeforeDeadline_IsNoOp()
    {
        var p = Product();
        var request = Request(new InventoryLine(p, 1));
        await Operations.ReserveAsync(request);
        await Operations.ExpireAsync(request.OrderId, request.ReservationRequestId);
        Assert.Single(_events.Messages);
        Assert.Equal(1, _store.Products[p].ReservedQuantity);
    }

    [Fact]
    public async Task ExpiryAfterDeadline_IsIdempotent()
    {
        var p = Product();
        var request = Request(new InventoryLine(p, 1));
        await Operations.ReserveAsync(request);
        _clock.Now = request.ExpiresAtUtc;
        await Operations.ExpireAsync(request.OrderId, request.ReservationRequestId);
        await Operations.ExpireAsync(request.OrderId, request.ReservationRequestId);
        Assert.Equal(2, _events.Messages.Count);
        Assert.IsType<InventoryExpired>(_events.Messages.Last());
        Assert.Equal(0, _store.Products[p].ReservedQuantity);
    }

    [Fact]
    public async Task SecondAttemptForSameOrder_RequiresFirstToBeReleased()
    {
        var p = Product();
        var first = Request(new InventoryLine(p, 2));
        await Operations.ReserveAsync(first);
        var second = first with { ReservationRequestId = Guid.NewGuid() };
        await Operations.ReserveAsync(second);
        Assert.Equal("OrderAlreadyReservedOrCommitted", Assert.IsType<InventoryRejected>(_events.Messages.Last()).Reason);
        await Operations.ReleaseAsync(new(first.OrderId, first.ReservationRequestId, InventoryReleaseReason.PaymentFailed));
        await Operations.ReserveAsync(first with { ReservationRequestId = Guid.NewGuid() });
        Assert.IsType<InventoryReserved>(_events.Messages.Last());
        Assert.Equal(2, _store.Products[p].ReservedQuantity);
    }

    [Fact]
    public async Task UnknownCommit_IsExplicitlyRejected()
    {
        await Operations.CommitAsync(new(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal("ReservationNotFound", Assert.IsType<InventoryCommandRejected>(Assert.Single(_events.Messages)).Reason);
    }

    [Fact]
    public async Task WrongOrderCannotFinalizeReservation()
    {
        var p = Product();
        var request = Request(new InventoryLine(p, 2));
        await Operations.ReserveAsync(request);
        await Operations.CommitAsync(new(Guid.NewGuid(), request.ReservationRequestId));
        Assert.IsType<InventoryCommandRejected>(_events.Messages.Last());
        Assert.Equal(2, _store.Products[p].ReservedQuantity);
    }

    [Fact]
    public async Task UndefinedReleaseReason_IsRejected()
        => await Assert.ThrowsAsync<ArgumentException>(() => Operations.ReleaseAsync(
            new(Guid.NewGuid(), Guid.NewGuid(), (InventoryReleaseReason)100)));

    [Fact]
    public void MalformedBatches_AreRejectedBeforeTransaction()
    {
        var p = Guid.NewGuid();
        InventoryLine[][] invalid = [[], [new(Guid.Empty, 1)], [new(p, 0)], [new(p, -1)],
            [new(p, 1), new(p, 2)], Enumerable.Range(0, 101).Select(_ => new InventoryLine(Guid.NewGuid(), 1)).ToArray(),
            [null!]];
        foreach (var items in invalid) Assert.Throws<ArgumentException>(() => InventoryOperations.ValidateItems(items));
        Assert.Throws<ArgumentException>(() => InventoryOperations.ValidateItems(null));
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class EventRecorder : IInventoryEventPublisher
    {
        public List<IntegrationEvent> Messages { get; } = [];
        public Task PublishAsync(IntegrationEvent message, CancellationToken cancellationToken)
        { Messages.Add(message); return Task.CompletedTask; }
    }

    // This fake exercises decisions, not transaction atomicity/concurrency (tested against SQL Server).
    private sealed class MemoryStore : IInventoryStore
    {
        public Dictionary<Guid, InventoryItem> Products { get; } = [];
        public Dictionary<Guid, ReservationAttempt> Attempts { get; } = [];
        public Dictionary<Guid, StockReceipt> Receipts { get; } = [];
        public Task ExecuteAsync(string lockKey, Func<Task> action, CancellationToken ct) => action();
        public Task LockAsync(string key, CancellationToken ct) => Task.CompletedTask;
        public Task<InventoryItem?> FindProductAsync(Guid id, CancellationToken ct) => Task.FromResult(Products.GetValueOrDefault(id));
        public void Add(InventoryItem item) => Products.Add(item.ProductId, item);
        public Task<ReservationAttempt?> FindAttemptAsync(Guid id, CancellationToken ct) => Task.FromResult(Attempts.GetValueOrDefault(id));
        public Task<bool> HasBlockingAttemptAsync(Guid id, CancellationToken ct)
            => Task.FromResult(Attempts.Values.Any(x => x.OrderId == id && x.Status is AttemptStatus.Reserved or AttemptStatus.Committed));
        public void Add(ReservationAttempt attempt) => Attempts.Add(attempt.Id, attempt);
        public Task<StockReceipt?> FindReceiptAsync(Guid id, CancellationToken ct) => Task.FromResult(Receipts.GetValueOrDefault(id));
        public void Add(StockReceipt receipt) => Receipts.Add(receipt.ReferenceId, receipt);
        public Task<IReadOnlyList<ExpiredAttempt>> GetExpiredAsync(DateTimeOffset now, int limit, CancellationToken ct)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<InventoryAvailability>> GetAvailabilityAsync(Guid[] ids, CancellationToken ct)
            => throw new NotSupportedException();
    }
}
