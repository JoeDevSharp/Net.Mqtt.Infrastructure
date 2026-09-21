using Net.Mqtt.Infrastructure;
using Net.Mqtt.Infrastructure.Attributes;
using Net.Mqtt.Infrastructure.Enums;
using Net.Mqtt.Infrastructure.RequestReply;
using Mint.Sales.WorkerService.OrderProcessing.Messaging.EventEntities;

namespace Mint.Sales.WorkerService.OrderProcessing.Messaging.Mqtt;

/// <summary>Déclare le modèle MQTT typé du service sans contenir de logique métier.</summary>
public sealed class OrderProcessingMqttContext(MqttContextDependencies dependencies) : MqttOrmContext(dependencies)
{
    /// <summary>Obtient le topic des commandes soumises consommées par le Worker.</summary>
    [MqttTopic(PublishTopic = "orders/submitted", SubscribeFilter = "orders/submitted", QoS = MqttQoS.AtLeastOnce)]
    public TopicSet<OrderSubmitted> SubmittedOrders => Set<OrderSubmitted>();

    /// <summary>Obtient le topic des résultats publiés après traitement.</summary>
    [MqttTopic(PublishTopic = "orders/processed", SubscribeFilter = "orders/processed", QoS = MqttQoS.AtLeastOnce)]
    public TopicSet<OrderProcessed> ProcessedOrders => Set<OrderProcessed>();

    /// <summary>Obtient le topic recevant les demandes de consultation.</summary>
    [MqttTopic(PublishTopic = "orders/read/request", SubscribeFilter = "orders/read/request", QoS = MqttQoS.AtLeastOnce)]
    public TopicSet<ReadOrder> ReadRequests => Set<ReadOrder>();

    /// <summary>Obtient le topic portant les réponses corrélées.</summary>
    [MqttTopic(PublishTopic = "orders/read/response", SubscribeFilter = "orders/read/response", QoS = MqttQoS.AtLeastOnce)]
    public TopicSet<OrderResponse> ReadResponses => Set<OrderResponse>();

    /// <summary>Obtient l’abstraction request/reply partagée qui attend le SUBACK avant publication.</summary>
    public MqttRequestSet<ReadOrder, OrderResponse> OrderRequest =>
        Request<ReadOrder, OrderResponse>(nameof(ReadRequests), nameof(ReadResponses));
}
