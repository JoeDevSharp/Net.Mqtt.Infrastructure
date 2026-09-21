using Net.Mqtt.Infrastructure.Contracts;

namespace Mint.Sales.WorkerService.OrderProcessing.Messaging.EventEntities;

/// <summary>Event Entity reçue lorsqu’une commande est soumise au service.</summary>
[EventType("com.mint.sales.order-processing.order-submitted.v1")]
[DataSchema("urn:schema:sales:order-processing:order-submitted:v1")]
[EventVersion("1.0.0")]
[MaximumDataSize(16 * 1024)]
[SchemaCompatibility(ContractCompatibility.SameMajor)]
[ForbiddenField("password")]
[ForbiddenField("secret")]
public sealed record OrderSubmitted
{
    /// <summary>Obtient l’identifiant métier de la commande.</summary>
    public required string OrderId { get; init; }
    /// <summary>Obtient l’identifiant du client.</summary>
    public required string CustomerId { get; init; }
    /// <summary>Obtient le montant total déclaré.</summary>
    public decimal Total { get; init; }
    /// <summary>Obtient l’instant de soumission.</summary>
    public DateTimeOffset SubmittedAt { get; init; }
}

/// <summary>Event Entity publiée après le traitement réussi d’une commande.</summary>
[EventType("com.mint.sales.order-processing.order-processed.v1")]
[DataSchema("urn:schema:sales:order-processing:order-processed:v1")]
[EventVersion("1.0.0")]
[MaximumDataSize(8 * 1024)]
public sealed record OrderProcessed
{
    /// <summary>Obtient l’identifiant de la commande traitée.</summary>
    public required string OrderId { get; init; }
    /// <summary>Obtient l’état métier sérialisé.</summary>
    public required string Status { get; init; }
    /// <summary>Obtient l’action recommandée au système consommateur.</summary>
    public required string Action { get; init; }
}

/// <summary>Event Entity de requête utilisée par le pattern MQTT request/reply.</summary>
[EventType("com.mint.sales.order-processing.read-order.v1")]
[DataSchema("urn:schema:sales:order-processing:read-order:v1")]
[EventVersion("1.0.0")]
[MaximumDataSize(4 * 1024)]
public sealed record ReadOrder
{
    /// <summary>Obtient l’identifiant de la commande recherchée.</summary>
    public required string OrderId { get; init; }
}

/// <summary>Event Entity de réponse corrélée à une requête <see cref="ReadOrder"/>.</summary>
[EventType("com.mint.sales.order-processing.order-response.v1")]
[DataSchema("urn:schema:sales:order-processing:order-response:v1")]
[EventVersion("1.0.0")]
[MaximumDataSize(8 * 1024)]
public sealed record OrderResponse
{
    /// <summary>Obtient l’identifiant demandé.</summary>
    public required string OrderId { get; init; }
    /// <summary>Obtient l’état courant ou <c>NotFound</c>.</summary>
    public required string Status { get; init; }
    /// <summary>Indique si la commande existe.</summary>
    public bool Found { get; init; }
}
