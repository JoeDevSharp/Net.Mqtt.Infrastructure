namespace Mint.Sales.WorkerService.OrderProcessing.Application.Abstractions.Persistence;

/// <summary>Indique si une livraison CloudEvent doit être traitée, ignorée ou laissée en cours.</summary>
public enum InboxDecision
{
    /// <summary>La livraison est réservée et le cas d’utilisation peut commencer.</summary>
    Started,
    /// <summary>La livraison a déjà produit tous ses effets et peut être acquittée sans les répéter.</summary>
    AlreadyCompleted,
    /// <summary>Une tentative récente possède encore le lease de traitement.</summary>
    AlreadyProcessing
}

/// <summary>Définit le port d’idempotence fondé sur l’identité CloudEvent <c>source + id</c>.</summary>
public interface IInbox
{
    /// <summary>Tente de réserver une livraison avant l’exécution de ses effets métier.</summary>
    Task<InboxDecision> TryBeginAsync(Uri source, string id, string type, CancellationToken cancellationToken);
    /// <summary>Marque la livraison terminée dans la même unité de travail que les changements métier.</summary>
    Task CompleteAsync(Uri source, string id, CancellationToken cancellationToken);
    /// <summary>Enregistre une erreur normalisée afin de permettre retry ou dead-letter.</summary>
    Task FailAsync(Uri source, string id, Exception exception, CancellationToken cancellationToken);
}
