using Microsoft.EntityFrameworkCore;
using Mint.Sales.WorkerService.OrderProcessing.Application.Abstractions.Persistence;
using Mint.Sales.WorkerService.OrderProcessing.Domain.Entities;

namespace Mint.Sales.WorkerService.OrderProcessing.Infrastructure.Persistence;

/// <summary>Adapte le port <see cref="IOrderRepository"/> à EF Core.</summary>
internal sealed class OrderRepository(OrdersDbContext db) : IOrderRepository
{
    /// <inheritdoc />
    public Task<Order?> GetAsync(string id, CancellationToken cancellationToken) =>
        db.Orders.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(Order order, CancellationToken cancellationToken) =>
        await db.Orders.AddAsync(order, cancellationToken);
}

/// <summary>Valide explicitement l’unité de travail EF Core partagée par les services scoped.</summary>
internal sealed class OrderUnitOfWork(OrdersDbContext db) : IOrderUnitOfWork
{
    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await db.SaveChangesAsync(cancellationToken);
}
