using MassTransit;
using ShopNet.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OrderService.IntegrationTests;

[Collection(OrderContainersCollection.Name)]
public class OrderRabbitMqIntegrationTests(OrderContainersFixture fixture)
{
    [Fact]
    public async Task Bus_PublishesMessageThroughRabbitMqContainer()
    {
        var received = new TaskCompletionSource<OrderBusProbe>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        builder.Services.AddMassTransit(registration => registration.UsingRabbitMq((_, configuration) =>
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
        }));
        using var host = builder.Build();
        var bus = host.Services.GetRequiredService<IBus>();
        await host.StartAsync();

        try
        {
            var message = new OrderBusProbe(Guid.NewGuid());
            await new Infrastructure.Bus(bus).PublishAsync(message);

            var consumed = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));

            Assert.Equal(message.CorrelationId, consumed.CorrelationId);
        }
        finally
        {
            await host.StopAsync();
        }
    }
}

public sealed record OrderBusProbe(Guid CorrelationId) : IntegrationEvent;
