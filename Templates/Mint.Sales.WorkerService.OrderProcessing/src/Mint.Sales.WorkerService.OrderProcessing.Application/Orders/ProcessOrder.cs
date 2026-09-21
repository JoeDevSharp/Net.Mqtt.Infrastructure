using Mint.Sales.WorkerService.OrderProcessing.Application.Abstractions.Persistence;
using Mint.Sales.WorkerService.OrderProcessing.Application.Abstractions.Services;
using Mint.Sales.WorkerService.OrderProcessing.Domain.Entities;
using Mint.Sales.WorkerService.OrderProcessing.Domain.Services;

namespace Mint.Sales.WorkerService.OrderProcessing.Application.Orders;

/// <summary>Commande applicative créée par un adaptateur à partir d’une soumission externe.</summary>
public sealed record ProcessOrderCommand(string OrderId, string CustomerId, decimal Total, DateTimeOffset SubmittedAt);
/// <summary>Résultat applicatif indépendant de MQTT et de tout modèle de persistance.</summary>
public sealed record ProcessOrderResult(string OrderId, OrderStatus Status, string Action);

/// <summary>Expose le cas d’utilisation de traitement idempotent d’une commande.</summary>
public interface IProcessOrder
{
    /// <summary>Valide, enrichit et enregistre la commande en propageant l’annulation.</summary>
    Task<ProcessOrderResult> ExecuteAsync(ProcessOrderCommand command, CancellationToken cancellationToken);
}

/// <summary>Orchestre les ports applicatifs et délègue les décisions à la couche Domain.</summary>
internal sealed class ProcessOrderHandler(IOrderRepository orders, ICustomerCatalogService customers) : IProcessOrder
{
    /// <inheritdoc />
    public async Task<ProcessOrderResult> ExecuteAsync(ProcessOrderCommand command, CancellationToken cancellationToken)
    {
        var existing = await orders.GetAsync(command.OrderId, cancellationToken);
        if (existing is not null) return new(existing.Id, existing.Status, "already-processed");

        var customer = await customers.GetAsync(command.CustomerId, cancellationToken);
        var order = new Order(command.OrderId, command.CustomerId, command.Total, command.SubmittedAt);
        var decision = OrderPolicy.Evaluate(order, customer);
        await orders.AddAsync(order, cancellationToken);
        return new(decision.OrderId, decision.Status, decision.Action);
    }
}
