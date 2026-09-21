using Mint.Sales.WorkerService.OrderProcessing.Domain.Entities;

namespace Mint.Sales.WorkerService.OrderProcessing.Application.Abstractions.Persistence;

/// <summary>Définit le port de persistance de l’agrégat commande sans imposer EF Core.</summary>
public interface IOrderRepository
{
    /// <summary>Recherche une commande par son identifiant métier.</summary>
    Task<Order?> GetAsync(string id, CancellationToken cancellationToken);
    /// <summary>Ajoute une nouvelle commande à l’unité de travail courante sans la valider immédiatement.</summary>
    Task AddAsync(Order order, CancellationToken cancellationToken);
}

/// <summary>Délimite la validation atomique des changements métier et de l’inbox.</summary>
public interface IOrderUnitOfWork
{
    /// <summary>Persiste tous les changements suivis dans l’unité de travail courante.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
