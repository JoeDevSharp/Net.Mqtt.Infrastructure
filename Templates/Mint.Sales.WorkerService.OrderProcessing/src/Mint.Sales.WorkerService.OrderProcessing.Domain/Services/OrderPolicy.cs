using Mint.Sales.WorkerService.OrderProcessing.Domain.Entities;

namespace Mint.Sales.WorkerService.OrderProcessing.Domain.Services;

/// <summary>Contient la règle métier pure qui décide du traitement d’une commande.</summary>
public static class OrderPolicy
{
    /// <summary>Évalue une commande selon l’état et la limite d’approbation du client.</summary>
    public static OrderDecision Evaluate(Order order, CustomerProfile customer) =>
        customer.IsActive && order.Total <= customer.AutomaticApprovalLimit
            ? new(order.Id, OrderStatus.Accepted, "dispatch")
            : new(order.Id, OrderStatus.ManualReview, "review");
}

/// <summary>Représente les informations client minimales requises par la règle métier.</summary>
public sealed record CustomerProfile(string Id, bool IsActive, decimal AutomaticApprovalLimit);
/// <summary>Représente le résultat immuable produit par la politique de commande.</summary>
public sealed record OrderDecision(string OrderId, OrderStatus Status, string Action);
