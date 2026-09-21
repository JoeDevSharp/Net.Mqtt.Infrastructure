using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mint.Sales.WorkerService.OrderProcessing.Application.Abstractions.Persistence;
using Mint.Sales.WorkerService.OrderProcessing.Application.Abstractions.Services;
using Mint.Sales.WorkerService.OrderProcessing.Infrastructure.ExternalServices;
using Mint.Sales.WorkerService.OrderProcessing.Infrastructure.Persistence;

namespace Mint.Sales.WorkerService.OrderProcessing.Infrastructure;

/// <summary>Regroupe les adaptateurs de persistance et de ressources externes.</summary>
public static class DependencyInjection
{
    /// <summary>Enregistre EF Core, l’inbox, les repositories et les clients HTTP typés.</summary>
    public static IServiceCollection AddOrderProcessingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OrdersDbContext>(options => options.UseInMemoryDatabase(
            configuration.GetValue<string>("Persistence:DatabaseName") ?? "order-processing"));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderUnitOfWork, OrderUnitOfWork>();
        services.AddScoped<IInbox, EfInbox>();
        services.AddHttpClient<ICustomerCatalogService, CustomerCatalogService>(client =>
        {
            client.BaseAddress = new Uri(configuration["CustomerCatalog:BaseUrl"]
                ?? throw new InvalidOperationException("Missing CustomerCatalog:BaseUrl."));
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        return services;
    }
}
