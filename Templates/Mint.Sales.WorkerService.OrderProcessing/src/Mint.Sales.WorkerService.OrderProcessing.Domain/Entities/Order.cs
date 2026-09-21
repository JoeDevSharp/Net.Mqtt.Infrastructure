namespace Mint.Sales.WorkerService.OrderProcessing.Domain.Entities;

/// <summary>Représente l’agrégat métier d’une commande, indépendant du transport et de la persistance.</summary>
public sealed class Order
{
    /// <summary>Constructeur réservé au matérialiseur EF Core.</summary>
    private Order() { }

    /// <summary>Crée une commande valide à partir des données acceptées par le cas d’utilisation.</summary>
    public Order(string id, string customerId, decimal total, DateTimeOffset submittedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        if (total <= 0) throw new ArgumentOutOfRangeException(nameof(total));
        Id = id;
        CustomerId = customerId;
        Total = total;
        SubmittedAt = submittedAt;
        Status = OrderStatus.Accepted;
    }

    /// <summary>Obtient l’identifiant métier stable de la commande.</summary>
    public string Id { get; private set; } = string.Empty;
    /// <summary>Obtient l’identifiant du client propriétaire de la commande.</summary>
    public string CustomerId { get; private set; } = string.Empty;
    /// <summary>Obtient le montant total de la commande.</summary>
    public decimal Total { get; private set; }
    /// <summary>Obtient la date de soumission exprimée avec son décalage UTC.</summary>
    public DateTimeOffset SubmittedAt { get; private set; }
    /// <summary>Obtient l’état métier courant de la commande.</summary>
    public OrderStatus Status { get; private set; }
}

/// <summary>Définit les décisions métier possibles après évaluation d’une commande.</summary>
public enum OrderStatus
{
    /// <summary>La commande peut poursuivre automatiquement son traitement.</summary>
    Accepted,
    /// <summary>La commande nécessite une décision humaine avant de poursuivre.</summary>
    ManualReview
}
