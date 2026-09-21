using Mint.Sales.WorkerService.OrderProcessing.Domain.Entities;
using Mint.Sales.WorkerService.OrderProcessing.Domain.Services;
using Xunit;

namespace Mint.Sales.WorkerService.OrderProcessing.Domain.Tests;

/// <summary>Vérifie les règles Domain sans DI, MQTT, HTTP ni base de données.</summary>
public sealed class OrderPolicyTests
{
    /// <summary>Vérifie qu’un client actif sous sa limite est accepté automatiquement.</summary>
    [Fact]
    public void Evaluate_AcceptsEligibleOrder()
    {
        var order = new Order("order-1", "customer-1", 50m, DateTimeOffset.UtcNow);
        var result = OrderPolicy.Evaluate(order, new CustomerProfile("customer-1", true, 100m));
        Assert.Equal(OrderStatus.Accepted, result.Status);
        Assert.Equal("dispatch", result.Action);
    }
}
