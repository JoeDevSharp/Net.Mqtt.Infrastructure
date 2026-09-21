using Microsoft.Extensions.DependencyInjection;
using Mint.Sales.WorkerService.OrderProcessing.Application.Abstractions.Persistence;
using Mint.Sales.WorkerService.OrderProcessing.Application.Abstractions.Services;
using Mint.Sales.WorkerService.OrderProcessing.Application.Orders;
using Mint.Sales.WorkerService.OrderProcessing.Domain.Entities;
using Mint.Sales.WorkerService.OrderProcessing.Domain.Services;
using Xunit;

namespace Mint.Sales.WorkerService.OrderProcessing.Application.Tests;

/// <summary>Vérifie l’orchestration Application avec des implémentations fake de ses ports.</summary>
public sealed class ProcessOrderTests
{
    /// <summary>Vérifie que le cas d’utilisation consulte ses ports et retourne la décision Domain.</summary>
    [Fact]
    public async Task ExecuteAsync_UsesPortsAndReturnsDecision()
    {
        var repository = new FakeOrderRepository();
        await using var provider = new ServiceCollection().AddOrderProcessingApplication()
            .AddSingleton<IOrderRepository>(repository).AddSingleton<ICustomerCatalogService, FakeCustomerCatalog>()
            .BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<IProcessOrder>();

        var result = await handler.ExecuteAsync(new("order-1", "customer-1", 42m, DateTimeOffset.UtcNow), CancellationToken.None);

        Assert.Equal(OrderStatus.Accepted, result.Status);
        Assert.NotNull(repository.Order);
    }

    private sealed class FakeOrderRepository : IOrderRepository
    {
        public Order? Order { get; private set; }
        public Task<Order?> GetAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Order);
        public Task AddAsync(Order order, CancellationToken cancellationToken) { Order = order; return Task.CompletedTask; }
    }

    private sealed class FakeCustomerCatalog : ICustomerCatalogService
    {
        public Task<CustomerProfile> GetAsync(string customerId, CancellationToken cancellationToken) =>
            Task.FromResult(new CustomerProfile(customerId, true, 100m));
    }
}
