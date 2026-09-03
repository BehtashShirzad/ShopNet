using MassTransit;
using ShopNet.Contracts;

namespace CatalogService.IntegrationTests;

[Collection(CatalogContainersCollection.Name)]
public class CatalogRabbitMqIntegrationTests(CatalogContainersFixture fixture)
{
    [Fact]
    public async Task Bus_PublishesMessageThroughRabbitMqContainer()
    {
        var received = new TaskCompletionSource<CatalogBusProbe>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var bus = MassTransit.Bus.Factory.CreateUsingRabbitMq(configuration =>
        {
            configuration.Host(new Uri(fixture.RabbitMqConnectionString));
            configuration.ReceiveEndpoint($"catalog-test-{Guid.NewGuid():N}", endpoint =>
            {
                endpoint.Handler<CatalogBusProbe>(context =>
                {
                    received.TrySetResult(context.Message);
                    return Task.CompletedTask;
                });
            });
        });
        await bus.StartAsync();

        try
        {
            var message = new CatalogBusProbe(Guid.NewGuid());
            await new Infrastructure.Bus(bus).PublishAsync(message);

            var consumed = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));

            Assert.Equal(message.CorrelationId, consumed.CorrelationId);
        }
        finally
        {
            await bus.StopAsync();
        }
    }
}

public sealed record CatalogBusProbe(Guid CorrelationId) : IntegrationEvent;
