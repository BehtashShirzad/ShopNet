using MassTransit;
using ShopNet.Contracts;

namespace OrderService.IntegrationTests;

[Collection(OrderContainersCollection.Name)]
public class OrderRabbitMqIntegrationTests(OrderContainersFixture fixture)
{
    [Fact]
    public async Task Bus_PublishesMessageThroughRabbitMqContainer()
    {
        var received = new TaskCompletionSource<OrderBusProbe>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var bus = MassTransit.Bus.Factory.CreateUsingRabbitMq(configuration =>
        {
            configuration.Host(new Uri(fixture.RabbitMqConnectionString));
            configuration.ReceiveEndpoint($"order-test-{Guid.NewGuid():N}", endpoint =>
            {
                endpoint.Handler<OrderBusProbe>(context =>
                {
                    received.TrySetResult(context.Message);
                    return Task.CompletedTask;
                });
            });
        });
        await bus.StartAsync();

        try
        {
            var message = new OrderBusProbe(Guid.NewGuid());
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

public sealed record OrderBusProbe(Guid CorrelationId) : IntegrationEvent;
