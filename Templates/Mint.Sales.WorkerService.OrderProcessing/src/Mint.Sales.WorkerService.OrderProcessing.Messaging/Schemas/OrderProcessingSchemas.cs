namespace Mint.Sales.WorkerService.OrderProcessing.Messaging.Schemas;

/// <summary>Centralise les JSON Schemas gouvernant chaque Event Entity du service.</summary>
internal static class OrderProcessingSchemas
{
    /// <summary>Schema de la commande soumise.</summary>
    internal const string OrderSubmitted = """
    {"$schema":"https://json-schema.org/draft/2020-12/schema","$id":"urn:schema:sales:order-processing:order-submitted:v1","type":"object","properties":{"orderId":{"type":"string","minLength":1},"customerId":{"type":"string","minLength":1},"total":{"type":"number","exclusiveMinimum":0},"submittedAt":{"type":"string","format":"date-time"}},"required":["orderId","customerId","total","submittedAt"],"additionalProperties":false}
    """;
    /// <summary>Schema du résultat de traitement.</summary>
    internal const string OrderProcessed = """
    {"$schema":"https://json-schema.org/draft/2020-12/schema","$id":"urn:schema:sales:order-processing:order-processed:v1","type":"object","properties":{"orderId":{"type":"string"},"status":{"type":"string"},"action":{"type":"string"}},"required":["orderId","status","action"],"additionalProperties":false}
    """;
    /// <summary>Schema de la requête de consultation.</summary>
    internal const string ReadOrder = """
    {"$schema":"https://json-schema.org/draft/2020-12/schema","$id":"urn:schema:sales:order-processing:read-order:v1","type":"object","properties":{"orderId":{"type":"string"}},"required":["orderId"],"additionalProperties":false}
    """;
    /// <summary>Schema de la réponse de consultation.</summary>
    internal const string OrderResponse = """
    {"$schema":"https://json-schema.org/draft/2020-12/schema","$id":"urn:schema:sales:order-processing:order-response:v1","type":"object","properties":{"orderId":{"type":"string"},"status":{"type":"string"},"found":{"type":"boolean"}},"required":["orderId","status","found"],"additionalProperties":false}
    """;
}
