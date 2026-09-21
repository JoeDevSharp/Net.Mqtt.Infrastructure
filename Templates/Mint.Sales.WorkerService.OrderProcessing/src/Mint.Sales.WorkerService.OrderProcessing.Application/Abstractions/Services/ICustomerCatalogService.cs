using Mint.Sales.WorkerService.OrderProcessing.Domain.Services;

namespace Mint.Sales.WorkerService.OrderProcessing.Application.Abstractions.Services;

/// <summary>Définit le gateway applicatif vers le catalogue client externe.</summary>
public interface ICustomerCatalogService
{
    /// <summary>Charge le profil minimal nécessaire au traitement d’une commande.</summary>
    Task<CustomerProfile> GetAsync(string customerId, CancellationToken cancellationToken);
}
