using Mint.Sales.WorkerService.OrderProcessing.Application.Orders;
using Mint.Sales.WorkerService.OrderProcessing.Messaging.EventEntities;
using Mint.Sales.WorkerService.OrderProcessing.Messaging.Mqtt;

namespace Mint.Sales.WorkerService.OrderProcessing.Worker.HostedServices;

/// <summary>Expose le cas d’utilisation de consultation au moyen du request/reply MQTT corrélé.</summary>
public sealed class CommandResponderWorker(
    OrderProcessingMqttContext mqtt,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    /// <summary>Démarre un responder partagé et propage le token d’arrêt à chaque requête.</summary>
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        mqtt.OrderRequest.HandleAsync(async (request, cancellationToken) =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var useCase = scope.ServiceProvider.GetRequiredService<IReadOrder>();
            var order = await useCase.ExecuteAsync(request.Data.OrderId, cancellationToken);
            return new OrderResponse
            {
                OrderId = request.Data.OrderId,
                Found = order is not null,
                Status = order?.Status.ToString() ?? "NotFound"
            };
        }, stoppingToken);
}
