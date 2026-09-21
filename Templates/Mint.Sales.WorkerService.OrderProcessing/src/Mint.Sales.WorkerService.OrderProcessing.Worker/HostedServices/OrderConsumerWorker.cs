using System.Diagnostics;
using Mint.Sales.WorkerService.OrderProcessing.Application.Abstractions.Persistence;
using Mint.Sales.WorkerService.OrderProcessing.Application.Orders;
using Mint.Sales.WorkerService.OrderProcessing.Messaging.EventEntities;
using Mint.Sales.WorkerService.OrderProcessing.Messaging.Mqtt;
using Net.Mqtt.Infrastructure.CloudEvents;
using Net.Mqtt.Infrastructure.Models;

namespace Mint.Sales.WorkerService.OrderProcessing.Worker.HostedServices;

/// <summary>
/// Adapte les commandes MQTT aux cas d’utilisation, crée un scope par message et n’acquitte
/// la livraison qu’après publication du résultat et validation de l’unité de travail.
/// </summary>
public sealed class OrderConsumerWorker(
    OrderProcessingMqttContext mqtt,
    IServiceScopeFactory scopeFactory,
    ILogger<OrderConsumerWorker> logger) : BackgroundService
{
    /// <summary>Maintient la souscription bornée jusqu’à l’arrêt coordonné du Host.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in mqtt.SubmittedOrders.ReadAllAsync(
            new SubscriptionOptions { Capacity = 32 }, stoppingToken))
        {
            var started = Stopwatch.GetTimestamp();
            using var logScope = logger.BeginScope(new Dictionary<string, object?>
            {
                ["CloudEventSource"] = message.CloudEvent.Source,
                ["CloudEventId"] = message.CloudEvent.Id,
                ["CloudEventType"] = message.CloudEvent.Type,
                ["CorrelationId"] = message.CloudEvent.Extensions.CorrelationId,
                ["Topic"] = message.Topic
            });

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var inbox = scope.ServiceProvider.GetRequiredService<IInbox>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IOrderUnitOfWork>();
                var decision = await inbox.TryBeginAsync(message.CloudEvent.Source, message.CloudEvent.Id,
                    message.CloudEvent.Type, stoppingToken);

                if (decision is InboxDecision.AlreadyCompleted)
                {
                    await message.AcknowledgeAsync(stoppingToken);
                    continue;
                }
                if (decision is InboxDecision.AlreadyProcessing)
                {
                    logger.LogWarning("Order message is already being processed; delivery remains unacknowledged.");
                    continue;
                }

                // Rend PROCESSING durable. La reprise d’un traitement abandonné est autorisée
                // après le délai de lease défini par l’inbox.
                await unitOfWork.SaveChangesAsync(stoppingToken);

                var useCase = scope.ServiceProvider.GetRequiredService<IProcessOrder>();
                var result = await useCase.ExecuteAsync(new ProcessOrderCommand(
                    message.Data.OrderId, message.Data.CustomerId, message.Data.Total, message.Data.SubmittedAt), stoppingToken);

                await mqtt.ProcessedOrders.PublishAsync(new OrderProcessed
                {
                    OrderId = result.OrderId,
                    Status = result.Status.ToString(),
                    Action = result.Action
                }, new CloudEventPublishOptions
                {
                    Context = new CloudEventPublishContext
                    {
                        Extensions = new CloudEventExtensions
                        {
                            CorrelationId = message.CloudEvent.Extensions.CorrelationId,
                            CausationId = message.CloudEvent.Id,
                            TraceParent = message.CloudEvent.Extensions.TraceParent,
                            TraceState = message.CloudEvent.Extensions.TraceState
                        }
                    }
                }, stoppingToken);

                await inbox.CompleteAsync(message.CloudEvent.Source, message.CloudEvent.Id, stoppingToken);
                await unitOfWork.SaveChangesAsync(stoppingToken);
                await message.AcknowledgeAsync(stoppingToken);
                logger.LogInformation("Order completed in {ElapsedMs} ms.", Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Order processing failed; message was not acknowledged and can be redelivered.");
                try
                {
                    await using var failureScope = scopeFactory.CreateAsyncScope();
                    var inbox = failureScope.ServiceProvider.GetRequiredService<IInbox>();
                    await inbox.FailAsync(message.CloudEvent.Source, message.CloudEvent.Id, exception, stoppingToken);
                    await failureScope.ServiceProvider.GetRequiredService<IOrderUnitOfWork>().SaveChangesAsync(stoppingToken);
                }
                catch (Exception inboxException)
                {
                    logger.LogError(inboxException, "Unable to persist inbox failure state.");
                }
            }
        }
    }
}
