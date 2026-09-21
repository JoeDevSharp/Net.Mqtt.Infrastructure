using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mint.Sales.WorkerService.OrderProcessing.Messaging;
using Mint.Sales.WorkerService.OrderProcessing.Messaging.EventEntities;
using Mint.Sales.WorkerService.OrderProcessing.Messaging.Mqtt;
using Xunit;

namespace Mint.Sales.WorkerService.OrderProcessing.Worker.IntegrationTests;

/// <summary>Vérifie ensemble le registre, le schema, le codec CloudEvents et le transport MQTT.</summary>
public sealed class MessagingTests
{
    /// <summary>Vérifie qu’une Event Entity valide traverse le transport InMemory et peut être acquittée.</summary>
    [Fact]
    public async Task InMemoryTransport_ValidatesAndDeliversEventEntity()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var values = new Dictionary<string, string?>
        {
            ["Mqtt:Host"] = "unused", ["Mqtt:Port"] = "1883", ["Mqtt:ClientId"] = "integration-test",
            ["Mqtt:BaseTopic"] = "tests/v1", ["Mqtt:ModuleIdentity"] = "sales",
            ["Mqtt:ServiceIdentity"] = "order-processing", ["Mqtt:CloudEventSource"] = "urn:tests:order-processing",
            ["Mqtt:UseInMemoryTransport"] = "true"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddOrderProcessingMessaging(configuration);
        using var host = builder.Build();
        await host.StartAsync(timeout.Token);
        var mqtt = host.Services.GetRequiredService<OrderProcessingMqttContext>();

        await using var messages = mqtt.SubmittedOrders.ReadAllAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
        var pending = messages.MoveNextAsync().AsTask();
        await mqtt.SubmittedOrders.PublishAsync(new OrderSubmitted
        {
            OrderId = "order-1", CustomerId = "customer-1", Total = 10m, SubmittedAt = DateTimeOffset.UtcNow
        }, timeout.Token);

        Assert.True(await pending);
        Assert.Equal("order-1", messages.Current.Data.OrderId);
        await messages.Current.AcknowledgeAsync(timeout.Token);
        await host.StopAsync(timeout.Token);
    }
}
