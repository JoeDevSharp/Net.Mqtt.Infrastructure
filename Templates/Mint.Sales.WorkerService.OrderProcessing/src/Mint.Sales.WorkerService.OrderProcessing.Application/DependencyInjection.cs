using Microsoft.Extensions.DependencyInjection;
using Mint.Sales.WorkerService.OrderProcessing.Application.Orders;

namespace Mint.Sales.WorkerService.OrderProcessing.Application;

/// <summary>Regroupe les enregistrements DI propres à la couche Application.</summary>
public static class DependencyInjection
{
    /// <summary>Enregistre les cas d’utilisation sans référencer leurs adaptateurs Infrastructure.</summary>
    public static IServiceCollection AddOrderProcessingApplication(this IServiceCollection services) => services
        .AddScoped<IProcessOrder, ProcessOrderHandler>()
        .AddScoped<IReadOrder, ReadOrderHandler>();
}
