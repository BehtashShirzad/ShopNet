using System.Linq.Expressions;
using Application.Abstractions.Contracts;
using MassTransit;
using MediatR;
using Moq;
using OrderService.Application.Commands;
using OrderService.Application.DomainEventHandler;
using OrderService.Application.IntegrationEventHandler;
using OrderService.Application.Query.GetOrderById;
using OrderService.Domain;
using OrderService.Domain.Aggregates;
using OrderService.Domain.DomainEvents;
using ShopNet.Contracts.IntegrationEvents;
using ShopNet.Contracts.SharedDtos;
using Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Application.Inventory;

namespace OrderService.UnitTests;

public class OrderApplicationTests
{
    [Fact]
    public async Task CreateOrder_IsIdempotentByCartId()
    {
        var command = NewCommand(Guid.NewGuid());
        var existing = OrderAggregate.Create(command.CustomerId, command.CartId);
        foreach (var item in command.Items)
            existing.AddItem(item.ProductId, item.ProductName, item.Price, item.Quantity);
        var repository = new Mock<IOrderRepository>();
        repository.Setup(x => x.GetByCartId(existing.CartId)).ReturnsAsync(existing);

        await CreateHandler(repository.Object).Handle(command, CancellationToken.None);

        repository.Verify(x => x.AddAsync(It.IsAny<OrderAggregate>()), Times.Never);
    }

    [Fact]
    public async Task CreateOrder_AddsItemsAndRaisesCompleteCreatedEvent()
    {
        OrderAggregate? added = null;
        var repository = new Mock<IOrderRepository>();
        repository.Setup(x => x.GetByCartId(It.IsAny<Guid>()))
            .ReturnsAsync((OrderAggregate?)null);
        repository.Setup(x => x.AddAsync(It.IsAny<OrderAggregate>()))
            .Callback<OrderAggregate>(order => added = order)
            .Returns(Task.CompletedTask);
        var command = NewCommand(Guid.NewGuid());

        await CreateHandler(repository.Object)
            .Handle(command, CancellationToken.None);

        Assert.NotNull(added);
        Assert.Equal(command.CustomerId, added.CustomerId);
        Assert.Equal(30m, added.TotalPrice);
        Assert.Equal(2, added.Items.Count);
        var domainEvent = Assert.IsType<OrderCreatedDomainEvent>(
            Assert.Single(added.DomainEvents));
        Assert.Equal(added.Id, domainEvent.OrderId);
        Assert.Equal(2, domainEvent.OrderItems.Count);
    }

    [Fact]
    public async Task OrderCreatedHandler_PublishesMappedIntegrationEvent()
    {
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var domainEvent = new OrderCreatedDomainEvent(orderId,
            [new(productId, "Product", 10m, 2)], customerId);
        OrderCreatedEvent? published = null;
        var eventBus = new Mock<IIntegrationEventBus>();
        eventBus.Setup(x => x.PublishAsync(
                It.IsAny<OrderCreatedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<OrderCreatedEvent, CancellationToken>((message, _) => published = message)
            .Returns(Task.CompletedTask);

        await new OrderCreatedDomainEventHandler(eventBus.Object).Handle(
            new DomainEventNotification<OrderCreatedDomainEvent>(domainEvent),
            CancellationToken.None);

        Assert.NotNull(published);
        Assert.Equal(orderId, published.OrderId);
        Assert.Equal(customerId, published.CustomerId);
        Assert.Equal(productId, Assert.Single(published.Items).ProductId);
    }

    [Fact]
    public async Task CartCheckedOutConsumer_SendsCreateOrderCommand()
    {
        var message = new CartCheckedOutEvent(
            Guid.NewGuid(), Guid.NewGuid(),
            [new ProductDto(Guid.NewGuid(), "Product", 10m, 2)], 20m);
        var context = new Mock<ConsumeContext<CartCheckedOutEvent>>();
        context.SetupGet(x => x.Message).Returns(message);
        CreateOrderCommand? sent = null;
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(
                It.IsAny<CreateOrderCommand>(), It.IsAny<CancellationToken>()))
            .Callback<CreateOrderCommand, CancellationToken>((command, _) => sent = command)
            .Returns(Task.CompletedTask);

        using var services = new ServiceCollection().AddSingleton(sender.Object).BuildServiceProvider();
        await new CartCheckedOutEventHandler(services.GetRequiredService<IServiceScopeFactory>()).Consume(context.Object);

        Assert.NotNull(sent);
        Assert.Equal(message.CartId, sent.CartId);
        Assert.Equal(message.CustomerId, sent.CustomerId);
        Assert.Equal(message.Items, sent.Items);
    }

    [Fact]
    public async Task GetOrder_ReturnsMappedOwnedOrder()
    {
        var customerId = Guid.NewGuid();
        var order = OrderAggregate.Create(customerId, Guid.NewGuid());
        order.AddItem(Guid.NewGuid(), "Product", 10m, 2);
        var repository = new Mock<IOrderRepository>();
        repository.Setup(x => x.GetAsync(
                It.IsAny<Expression<Func<OrderAggregate, bool>>>()))
            .ReturnsAsync(order);

        var response = await new GetOrderByIdQueryHandler(repository.Object)
            .Handle(new GetOrderByIdQuery(order.Id, customerId), CancellationToken.None);

        Assert.Equal(order.Id, response.OrderId);
        Assert.Equal(order.Status, response.OrderStatus);
        Assert.Equal("Product", Assert.Single(response.ProductDto).ProductName);
    }

    [Fact]
    public async Task GetOrder_ThrowsWhenRepositoryReturnsNoOrder()
    {
        var repository = new Mock<IOrderRepository>();
        repository.Setup(x => x.GetAsync(
                It.IsAny<Expression<Func<OrderAggregate, bool>>>()))
            .ReturnsAsync((OrderAggregate?)null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GetOrderByIdQueryHandler(repository.Object).Handle(
                new GetOrderByIdQuery(Guid.NewGuid(), Guid.NewGuid()),
                CancellationToken.None));

        Assert.Equal("Order not found", exception.Message);
    }

    private static CreateOrderCommandHandler CreateHandler(IOrderRepository repository) => new(repository,
        Mock.Of<IInventoryCommandSender>(), Mock.Of<IOrderTransactionLock>(), TimeProvider.System, new OrderInventoryOptions());

    private static CreateOrderCommand NewCommand(Guid cartId) => new(
        cartId,
        Guid.NewGuid(),
        [
            new ProductDto(Guid.NewGuid(), "First", 10m, 1),
            new ProductDto(Guid.NewGuid(), "Second", 10m, 2)
        ],
        30m);
}
