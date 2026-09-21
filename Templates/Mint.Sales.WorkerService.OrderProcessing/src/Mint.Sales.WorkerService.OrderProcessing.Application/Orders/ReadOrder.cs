using Mint.Sales.WorkerService.OrderProcessing.Application.Abstractions.Persistence;
using Mint.Sales.WorkerService.OrderProcessing.Domain.Entities;

namespace Mint.Sales.WorkerService.OrderProcessing.Application.Orders;

/// <summary>Expose le cas d’utilisation de consultation d’une commande.</summary>
public interface IReadOrder
{
    /// <summary>Retourne la commande demandée ou <see langword="null"/> lorsqu’elle n’existe pas.</summary>
    Task<Order?> ExecuteAsync(string orderId, CancellationToken cancellationToken);
}

/// <summary>Implémente la consultation en utilisant exclusivement le port de persistance applicatif.</summary>
internal sealed class ReadOrderHandler(IOrderRepository orders) : IReadOrder
{
    /// <inheritdoc />
    public Task<Order?> ExecuteAsync(string orderId, CancellationToken cancellationToken) =>
        orders.GetAsync(orderId, cancellationToken);
}
