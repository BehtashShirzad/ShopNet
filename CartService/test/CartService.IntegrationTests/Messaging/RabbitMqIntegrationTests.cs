using MassTransit;
using ShopNet.Contracts;

namespace CartService.IntegrationTests;

[Collection(CartContainersCollection.Name)]
public class RabbitMqIntegrationTests(CartContainersFixture fixture)
{
    [Fact]
    public async Task Bus_PublishesMessageThroughRabbitMqContainer()
    {
        var received = new TaskCompletionSource<CartBusProbe>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var bus = MassTransit.Bus.Factory.CreateUsingRabbitMq(configuration =>
        {
            configuration.Host(new Uri(fixture.RabbitMqConnectionString));
            configuration.ReceiveEndpoint($"cart-test-{Guid.NewGuid():N}", endpoint =>
            {
                endpoint.Handler<CartBusProbe>(context =>
                {
                    received.TrySetResult(context.Message);
                    return Task.CompletedTask;
                });
            });
        });
        await bus.StartAsync();

        try
        {
            var message = new CartBusProbe(Guid.NewGuid());

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

public sealed record CartBusProbe(Guid CorrelationId) : IntegrationEvent;
