using Application.Abstractions.Contracts;
using Domain.Abstractions;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrderService.Application.Commands;
using OrderService.Application.IntegrationEventHandler;
using OrderService.Application.Inventory;
using OrderService.Domain;
using OrderService.Domain.Aggregates;
using OrderService.Domain.Enums;
using OrderService.Infrastructure;
using ShopNet.Contracts.Inventory.V1;
using ShopNet.Contracts.SharedDtos;

namespace OrderService.UnitTests;

public sealed class OrderInventoryApplicationTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private static CreateOrderCommand Checkout() => new(Guid.NewGuid(), Guid.NewGuid(),
        [new ProductDto(Guid.NewGuid(), "P", 12, 2)], 24);

    [Fact]
    public async Task CreateSendsOneStableCorrelatedReserveAndUsesConfiguredDeadline()
    {
        var repository = new Mock<IOrderRepository>();
        var sender = new Mock<IInventoryCommandSender>();
        var locks = new Mock<IOrderTransactionLock>();
        OrderAggregate? order = null;
        ReserveInventory? reserve = null;
        repository.Setup(x => x.AddAsync(It.IsAny<OrderAggregate>())).Callback<OrderAggregate>(x => order = x).Returns(Task.CompletedTask);
        sender.Setup(x => x.ReserveAsync(It.IsAny<ReserveInventory>(), It.IsAny<CancellationToken>()))
            .Callback<ReserveInventory, CancellationToken>((x, _) => reserve = x).Returns(Task.CompletedTask);
        var command = Checkout();
        using var cancellation = new CancellationTokenSource();
        var handler = new CreateOrderCommandHandler(repository.Object, sender.Object, locks.Object, new Clock(),
            new OrderInventoryOptions { ReservationDuration = TimeSpan.FromMinutes(20) });
        await handler.Handle(command, cancellation.Token);
        Assert.NotNull(order);
        Assert.NotNull(reserve);
        Assert.Equal(order.Id, reserve.OrderId);
        Assert.Equal(order.InventoryReservationRequestId, reserve.ReservationRequestId);
        Assert.Equal(Now.AddMinutes(20), reserve.ExpiresAtUtc);
        Assert.Equal(command.Items[0].ProductId, Assert.Single(reserve.Items).ProductId);
        locks.Verify(x => x.AcquireAsync($"cart:{command.CartId:N}", cancellation.Token), Times.Once);
        repository.Setup(x => x.GetByCartId(command.CartId)).ReturnsAsync(order);
        await handler.Handle(command, cancellation.Token);
        sender.Verify(x => x.ReserveAsync(It.IsAny<ReserveInventory>(), cancellation.Token), Times.Once);
        Assert.Equal(Now.AddMinutes(20), order.InventoryReservationExpiresAtUtc);
    }

    [Theory]
    [InlineData("total")]
    [InlineData("empty")]
    [InlineData("null")]
    [InlineData("duplicate")]
    public async Task InvalidCheckoutCannotEnqueueOrPersist(string kind)
    {
        var command = Checkout();
        command = kind switch
        {
            "total" => command with { TotalPrice = 99 },
            "empty" => command with { Items = [] },
            "null" => command with { Items = null! },
            _ => command with { Items = [command.Items[0], command.Items[0]], TotalPrice = 48 }
        };
        var repository = new Mock<IOrderRepository>(MockBehavior.Strict);
        var sender = new Mock<IInventoryCommandSender>(MockBehavior.Strict);
        await Assert.ThrowsAsync<ArgumentException>(() => new CreateOrderCommandHandler(repository.Object, sender.Object,
            Mock.Of<IOrderTransactionLock>(), new Clock(), new()).Handle(command, default));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DuplicateCartWithChangedCustomerOrLinesIsRejected(bool changeCustomer)
    {
        var command = Checkout();
        var order = OrderAggregate.Create(command.CustomerId, command.CartId);
        order.AddItem(command.Items[0].ProductId, "P", 12, 2);
        var repository = new Mock<IOrderRepository>();
        repository.Setup(x => x.GetByCartId(command.CartId)).ReturnsAsync(order);
        var changed = changeCustomer ? command with { CustomerId = Guid.NewGuid() }
            : command with { Items = [command.Items[0] with { Quantity = 3 }], TotalPrice = 36 };
        await Assert.ThrowsAsync<ArgumentException>(() => new CreateOrderCommandHandler(repository.Object,
            Mock.Of<IInventoryCommandSender>(), Mock.Of<IOrderTransactionLock>(), new Clock(), new()).Handle(changed, default));
    }

    [Fact]
    public async Task ResultMustMatchItemsAndDeadline()
    {
        var order = OrderAggregate.Create(Guid.NewGuid(), Guid.NewGuid());
        var product = Guid.NewGuid();
        order.AddItem(product, "P", 12, 2);
        order.BeginInventoryReservation(Guid.NewGuid(), Now, TimeSpan.FromMinutes(15));
        var repository = new Mock<IOrderRepository>();
        repository.Setup(x => x.GetByIdAsync(order.Id)).ReturnsAsync(order);
        var handler = new ApplyInventoryResultCommandHandler(repository.Object, Mock.Of<IOrderTransactionLock>());
        var command = new ApplyInventoryResultCommand(order.Id, order.InventoryReservationRequestId!.Value,
            1, OrderInventoryStatus.Reserved, Items: [new(product, 3)], ExpiresAtUtc: order.InventoryReservationExpiresAtUtc);
        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, default));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command with
            { Items = [new(product, 2)], ExpiresAtUtc = Now }, default));
        await handler.Handle(command with { Items = [new(product, 2)] }, default);
        Assert.Equal(OrderStatus.InventoryReserved, order.Status);
    }

    [Fact]
    public async Task UnknownOrderFaultsButWrongAttemptIsIgnored()
    {
        var repository = new Mock<IOrderRepository>();
        var handler = new ApplyInventoryResultCommandHandler(repository.Object, Mock.Of<IOrderTransactionLock>());
        var command = new ApplyInventoryResultCommand(Guid.NewGuid(), Guid.NewGuid(), 1, OrderInventoryStatus.Rejected, "Missing");
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, default));
        var order = OrderAggregate.Create(Guid.NewGuid(), Guid.NewGuid());
        repository.Setup(x => x.GetByIdAsync(command.OrderId)).ReturnsAsync(order);
        await handler.Handle(command, default);
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public void ScopedContextAliasesAreIdenticalAndSeparateAcrossScopes()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:OrderServiceConnection"] = "Server=localhost;Database=not-used;Integrated Security=true" }).Build();
        var services = new ServiceCollection().AddLogging();
        OrderService.Application.DependencyInjection.AddApplicationServices(services);
        services.AddInfrastructureServices(configuration);
        using var provider = services.BuildServiceProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var db = first.ServiceProvider.GetRequiredService<WriteDbContext>();
        Assert.Same(db, first.ServiceProvider.GetRequiredService<IApplicationDbContext>());
        Assert.Same(db, first.ServiceProvider.GetRequiredService<IUnitOfWork>());
        Assert.NotSame(db, second.ServiceProvider.GetRequiredService<WriteDbContext>());
        Assert.Throws<NotSupportedException>(() => db.SaveChanges());
        Assert.Throws<NotSupportedException>(() => db.SaveChanges(false));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1441)]
    public void InvalidReservationConfigurationIsRejected(int minutes)
        => Assert.Throws<ArgumentException>(() => new OrderInventoryOptions
            { ReservationDuration = TimeSpan.FromMinutes(minutes) }.Validate());

    [Fact]
    public async Task ResultConsumerMapsAllOutcomesAndForwardsCancellation()
    {
        var sender = new Mock<ISender>();
        var sent = new List<object>();
        sender.Setup(x => x.Send(It.IsAny<ApplyInventoryResultCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ApplyInventoryResultCommand, CancellationToken>((x, _) => sent.Add(x)).Returns(Task.CompletedTask);
        sender.Setup(x => x.Send(It.IsAny<InventoryCommandRejectedCommand>(), It.IsAny<CancellationToken>()))
            .Callback<InventoryCommandRejectedCommand, CancellationToken>((x, _) => sent.Add(x)).Returns(Task.CompletedTask);
        using var provider = new ServiceCollection().AddSingleton(sender.Object).BuildServiceProvider();
        var consumer = new InventoryResultConsumer(provider.GetRequiredService<IServiceScopeFactory>());
        var order = Guid.NewGuid();
        var request = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        await consumer.Consume(Context(new InventoryReserved(order, request, [new(Guid.NewGuid(), 2)], Now)
            { ReservationVersion = 1 }, cancellation.Token));
        await consumer.Consume(Context(new InventoryRejected(order, request, "InsufficientStock") { ReservationVersion = 1 }, cancellation.Token));
        await consumer.Consume(Context(new InventoryReleased(order, request) { ReservationVersion = 2 }, cancellation.Token));
        await consumer.Consume(Context(new InventoryExpired(order, request) { ReservationVersion = 2 }, cancellation.Token));
        await consumer.Consume(Context(new InventoryCommitted(order, request) { ReservationVersion = 2 }, cancellation.Token));
        await consumer.Consume(Context(new InventoryCommandRejected(order, request, "Reserve", "Conflict"), cancellation.Token));
        Assert.Equal(new[] { OrderInventoryStatus.Reserved, OrderInventoryStatus.Rejected, OrderInventoryStatus.Released,
            OrderInventoryStatus.Expired, OrderInventoryStatus.Committed }, sent.OfType<ApplyInventoryResultCommand>().Select(x => x.Result));
        Assert.All(sent.OfType<ApplyInventoryResultCommand>(), x =>
        {
            Assert.Equal(order, x.OrderId);
            Assert.Equal(request, x.ReservationRequestId);
        });
        sender.Verify(x => x.Send(It.IsAny<ApplyInventoryResultCommand>(), cancellation.Token), Times.Exactly(5));
        Assert.Equal("Reserve", Assert.Single(sent.OfType<InventoryCommandRejectedCommand>()).Operation);
    }

    private static ConsumeContext<T> Context<T>(T message, CancellationToken ct) where T : class
    {
        var context = new Mock<ConsumeContext<T>>();
        context.SetupGet(x => x.Message).Returns(message);
        context.SetupGet(x => x.CancellationToken).Returns(ct);
        return context.Object;
    }
}
