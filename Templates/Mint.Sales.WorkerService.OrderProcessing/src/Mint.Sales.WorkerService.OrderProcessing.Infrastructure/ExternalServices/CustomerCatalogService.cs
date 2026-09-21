using System.Net.Http.Json;
using Mint.Sales.WorkerService.OrderProcessing.Application.Abstractions.Services;
using Mint.Sales.WorkerService.OrderProcessing.Domain.Services;

namespace Mint.Sales.WorkerService.OrderProcessing.Infrastructure.ExternalServices;

/// <summary>Adapte le catalogue client HTTP au port défini par Application.</summary>
internal sealed class CustomerCatalogService(HttpClient httpClient) : ICustomerCatalogService
{
    /// <inheritdoc />
    public async Task<CustomerProfile> GetAsync(string customerId, CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<CustomerProfile>($"customers/{Uri.EscapeDataString(customerId)}", cancellationToken)
        ?? throw new CustomerNotFoundException(customerId);
}

/// <summary>Signale qu’un identifiant client n’existe pas dans le catalogue externe.</summary>
public sealed class CustomerNotFoundException(string customerId) :
    Exception($"Customer '{customerId}' was not found.");
