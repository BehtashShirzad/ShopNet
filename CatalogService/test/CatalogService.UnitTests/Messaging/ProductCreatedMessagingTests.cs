using Application.Abstractions;
using Application.Abstractions.Contracts;
using CatalogService.Application;
using CatalogService.Application.Features.Product.DomainEventHandlers;
using CatalogService.Domain.DomainEvents;
using CatalogService.Infrastructure;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ProductCreatedV1 = ShopNet.Contracts.IntegrationEvents.Catalog.V1.ProductCreated;

namespace CatalogService.UnitTests;

public sealed class ProductCreatedMessagingTests
{
    [Fact]
    public async Task PersistenceInterfaceAndOutbox_ResolveSameDbContextWithinScope()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddApplicationServices();
        services.AddInfraService(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CatalogServiceConnection"] =
                    "Server=localhost;Database=NotConnected;Integrated Security=true;TrustServerCertificate=true",
                ["RabbitMq:Host"] = "localhost",
                ["RabbitMq:Port"] = "5672",
                ["RabbitMq:VirtualHost"] = "/",
                ["RabbitMq:Username"] = "guest",
                ["RabbitMq:Password"] = "guest"
            }).Build());
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var concrete = scope.ServiceProvider.GetRequiredService<WriteDbContext>();
        var abstraction = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        Assert.Same(concrete, abstraction);
        await using var otherScope = provider.CreateAsyncScope();
        Assert.NotSame(concrete, otherScope.ServiceProvider.GetRequiredService<WriteDbContext>());
    }

    [Fact]
    public async Task Handler_MapsProductAndStableEventMetadataAndForwardsCancellation()
    {
        var domainEvent = new ProductCreatedDomainEvent(Guid.NewGuid());
        using var cancellation = new CancellationTokenSource();
        var bus = new Mock<IIntegrationEventBus>();
        bus.Setup(x => x.PublishAsync(It.IsAny<ProductCreatedV1>(), cancellation.Token))
            .Returns(Task.CompletedTask);
        var handler = new ProductCreatedDomainEventHandler(bus.Object);

        await handler.Handle(new DomainEventNotification<ProductCreatedDomainEvent>(domainEvent),
            cancellation.Token);

        bus.Verify(x => x.PublishAsync(
            It.Is<ProductCreatedV1>(message =>
                message.ProductId == domainEvent.ProductId &&
                message.EventId == domainEvent.Id &&
                message.OccurredOnUtc == new DateTimeOffset(domainEvent.OccurredOn)),
            cancellation.Token), Times.Once);
        bus.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handler_ReplayKeepsSameBusinessEventId()
    {
        var domainEvent = new ProductCreatedDomainEvent(Guid.NewGuid());
        var bus = new Mock<IIntegrationEventBus>();
        var messages = new List<ProductCreatedV1>();
        bus.Setup(x => x.PublishAsync(It.IsAny<ProductCreatedV1>(), It.IsAny<CancellationToken>()))
            .Callback<ProductCreatedV1, CancellationToken>((message, _) => messages.Add(message))
            .Returns(Task.CompletedTask);
        var handler = new ProductCreatedDomainEventHandler(bus.Object);
        var notification = new DomainEventNotification<ProductCreatedDomainEvent>(domainEvent);

        await handler.Handle(notification, default);
        await handler.Handle(notification, default);

        Assert.Equal(2, messages.Count);
        Assert.Equal(messages[0], messages[1]);
    }

    [Fact]
    public async Task Handler_DoesNotSwallowOutboxEnqueueFailure()
    {
        var failure = new InvalidOperationException("Outbox unavailable");
        var bus = new Mock<IIntegrationEventBus>();
        bus.Setup(x => x.PublishAsync(It.IsAny<ProductCreatedV1>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ProductCreatedDomainEventHandler(bus.Object).Handle(
                new DomainEventNotification<ProductCreatedDomainEvent>(
                    new ProductCreatedDomainEvent(Guid.NewGuid())), default));

        Assert.Same(failure, actual);
    }

    [Fact]
    public async Task IntegrationBus_UsesScopedPublishEndpoint()
    {
        var endpoint = new Mock<IPublishEndpoint>();
        var message = new ProductCreatedV1(Guid.NewGuid());
        using var cancellation = new CancellationTokenSource();
        endpoint.Setup(x => x.Publish((object)message, cancellation.Token)).Returns(Task.CompletedTask);

        await new CatalogService.Infrastructure.Bus(endpoint.Object)
            .PublishAsync(message, cancellation.Token);

        endpoint.Verify(x => x.Publish((object)message, cancellation.Token), Times.Once);
        endpoint.VerifyNoOtherCalls();
    }

    [Fact]
    public void DomainEvent_HasDistinctEventAndProductIdentities()
    {
        var productId = Guid.NewGuid();
        var first = new ProductCreatedDomainEvent(productId);
        var second = new ProductCreatedDomainEvent(productId);

        Assert.Equal(productId, first.ProductId);
        Assert.NotEqual(productId, first.Id);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(DateTimeKind.Utc, first.OccurredOn.Kind);
        Assert.Throws<ArgumentException>(() => new ProductCreatedDomainEvent(Guid.Empty));
    }

    [Fact]
    public void WriteModel_ContainsTransactionalOutboxTables()
    {
        using var context = CreateContext();

        Assert.NotNull(context.Model.FindEntityType(typeof(OutboxMessage)));
        Assert.NotNull(context.Model.FindEntityType(typeof(OutboxState)));
        Assert.NotNull(context.Model.FindEntityType(typeof(InboxState)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SynchronousSave_IsRejectedSoItCannotBypassDomainEventDispatch(bool acceptChanges)
    {
        using var context = CreateContext();

        Assert.Throws<NotSupportedException>(() => context.SaveChanges(acceptChanges));
        Assert.Throws<NotSupportedException>(() => context.SaveChanges());
    }

    [Fact]
    public void CatalogApi_DoesNotReferenceAnotherServiceImplementation()
    {
        var dependencies = typeof(CatalogService.Api.Routes.ProductEndpoint)
            .Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();

        Assert.DoesNotContain(dependencies, name =>
            name is not null &&
            (name.StartsWith("CartService.") || name.StartsWith("InventoryService.") ||
             name.StartsWith("OrderService.")));
    }

    private static WriteDbContext CreateContext() => new(
        new DbContextOptionsBuilder<WriteDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Integrated Security=true;TrustServerCertificate=true")
            .Options,
        Mock.Of<IDomainEventBus>(),
        Mock.Of<ICurrentUser>());
}
