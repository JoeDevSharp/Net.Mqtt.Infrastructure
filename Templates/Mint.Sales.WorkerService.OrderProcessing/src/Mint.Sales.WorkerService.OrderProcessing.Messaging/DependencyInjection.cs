using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mint.Sales.WorkerService.OrderProcessing.Messaging.Configuration;
using Mint.Sales.WorkerService.OrderProcessing.Messaging.EventEntities;
using Mint.Sales.WorkerService.OrderProcessing.Messaging.Mqtt;
using Mint.Sales.WorkerService.OrderProcessing.Messaging.Schemas;

namespace Mint.Sales.WorkerService.OrderProcessing.Messaging;

/// <summary>Compose le transport, les Event Entities, les schemas et le contexte MQTT.</summary>
public static class DependencyInjection
{
    /// <summary>Enregistre la couche Messaging et valide sa configuration au démarrage.</summary>
    public static IServiceCollection AddOrderProcessingMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MqttSettings>()
            .Bind(configuration.GetRequiredSection(MqttSettings.SectionName))
            .ValidateDataAnnotations()
            .Validate(x => Uri.TryCreate(x.CloudEventSource, UriKind.Absolute, out _), "CloudEventSource must be absolute.")
            .ValidateOnStart();

        var settings = configuration.GetRequiredSection(MqttSettings.SectionName).Get<MqttSettings>()
            ?? throw new InvalidOperationException("Missing Mqtt configuration.");

        services.AddMqttReactiveOrm<OrderProcessingMqttContext>(mqtt =>
        {
            if (settings.UseInMemoryTransport) 
                mqtt.UseInMemoryTransport();

            mqtt.ConnectTo(settings.Host, settings.Port)
                .IdentifyAs(settings.ClientId)
                .WithBaseTopic(settings.BaseTopic)
                .ForModule(settings.ModuleIdentity)
                .ForService(settings.ServiceIdentity)
                .WithCloudEventSource(settings.CloudEventSource)
                .UseProductionDefaults();

            mqtt.UseEventEntities(entities => 
            {
                entities.Add<OrderSubmitted>(); 
                entities.Add<OrderProcessed>();
                entities.Add<ReadOrder>();
                entities.Add<OrderResponse>(); 
            });

            mqtt.UseSchemas(schemas =>
            {
                schemas.AddInline("urn:schema:sales:order-processing:order-submitted:v1", OrderProcessingSchemas.OrderSubmitted, "1.0.0");
                schemas.AddInline("urn:schema:sales:order-processing:order-processed:v1", OrderProcessingSchemas.OrderProcessed, "1.0.0");
                schemas.AddInline("urn:schema:sales:order-processing:read-order:v1", OrderProcessingSchemas.ReadOrder, "1.0.0");
                schemas.AddInline("urn:schema:sales:order-processing:order-response:v1", OrderProcessingSchemas.OrderResponse, "1.0.0");
            });
        });
        return services;
    }
}
