using Microsoft.EntityFrameworkCore;
using Mint.Sales.WorkerService.OrderProcessing.Application.Abstractions.Persistence;

namespace Mint.Sales.WorkerService.OrderProcessing.Infrastructure.Persistence;

/// <summary>Implémente l’inbox durable dans le même DbContext que les effets métier.</summary>
internal sealed class EfInbox(OrdersDbContext db, TimeProvider clock) : IInbox
{
    /// <inheritdoc />
    public async Task<InboxDecision> TryBeginAsync(Uri source, string id, string type, CancellationToken cancellationToken)
    {
        var sourceValue = source.AbsoluteUri;
        var entry = await db.Inbox.SingleOrDefaultAsync(x => x.Source == sourceValue && x.Id == id, cancellationToken);
        var now = clock.GetUtcNow();
        if (entry?.Status is InboxStatus.Completed) return InboxDecision.AlreadyCompleted;
        if (entry?.Status is InboxStatus.Processing && now - entry.LastAttemptAt < TimeSpan.FromMinutes(5))
            return InboxDecision.AlreadyProcessing;
        if (entry is null)
        {
            entry = new InboxEntry { Source = sourceValue, Id = id, Type = type, ReceivedAt = now };
            await db.Inbox.AddAsync(entry, cancellationToken);
        }
        entry.Status = InboxStatus.Processing;
        entry.Attempts++;
        entry.LastAttemptAt = now;
        entry.LastException = null;
        return InboxDecision.Started;
    }

    /// <inheritdoc />
    public async Task CompleteAsync(Uri source, string id, CancellationToken cancellationToken)
    {
        var entry = await RequiredAsync(source, id, cancellationToken);
        entry.Status = InboxStatus.Completed;
        entry.CompletedAt = clock.GetUtcNow();
    }

    /// <inheritdoc />
    public async Task FailAsync(Uri source, string id, Exception exception, CancellationToken cancellationToken)
    {
        var entry = await RequiredAsync(source, id, cancellationToken);
        entry.Status = entry.Attempts >= 5 ? InboxStatus.DeadLettered : InboxStatus.RetryScheduled;
        entry.LastException = Normalize(exception);
    }

    /// <summary>Charge une entrée existante ou signale une violation du protocole de l’inbox.</summary>
    private async Task<InboxEntry> RequiredAsync(Uri source, string id, CancellationToken cancellationToken) =>
        await db.Inbox.SingleOrDefaultAsync(x => x.Source == source.AbsoluteUri && x.Id == id, cancellationToken)
        ?? throw new InvalidOperationException($"Inbox entry '{source}/{id}' was not started.");

    /// <summary>Réduit une exception à un type et un message borné, sans persister sa stack trace.</summary>
    private static string Normalize(Exception exception)
    {
        var value = $"{exception.GetType().Name}: {exception.Message}";
        return value.Length <= 2048 ? value : value[..2048];
    }
}
