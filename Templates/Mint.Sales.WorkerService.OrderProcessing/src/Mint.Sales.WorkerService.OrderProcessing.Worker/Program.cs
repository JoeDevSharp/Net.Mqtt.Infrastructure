using Mint.Sales.WorkerService.OrderProcessing.Application;
using Mint.Sales.WorkerService.OrderProcessing.Infrastructure;
using Mint.Sales.WorkerService.OrderProcessing.Messaging;
using Mint.Sales.WorkerService.OrderProcessing.Worker.HostedServices;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOrderProcessingApplication()
    .AddOrderProcessingInfrastructure(builder.Configuration)
    .AddOrderProcessingMessaging(builder.Configuration);

builder.Services.AddHostedService<OrderConsumerWorker>();
builder.Services.AddHostedService<CommandResponderWorker>();

await builder.Build().RunAsync();
