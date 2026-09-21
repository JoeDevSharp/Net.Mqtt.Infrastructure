using Microsoft.EntityFrameworkCore;
using Mint.Sales.WorkerService.OrderProcessing.Domain.Entities;

namespace Mint.Sales.WorkerService.OrderProcessing.Infrastructure.Persistence;

/// <summary>Unité de travail EF Core qui coordonne commandes et inbox dans une même base.</summary>
internal sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    /// <summary>Obtient l’ensemble persistant des commandes.</summary>
    internal DbSet<Order> Orders => Set<Order>();
    /// <summary>Obtient l’ensemble persistant des identités CloudEvent déjà reçues.</summary>
    internal DbSet<InboxEntry> Inbox => Set<InboxEntry>();

    /// <summary>Configure les clés, conversions, précisions et contraintes du modèle.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(128);
            entity.Property(x => x.CustomerId).HasMaxLength(128);
            entity.Property(x => x.Total).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        });
        modelBuilder.Entity<InboxEntry>(entity =>
        {
            entity.HasKey(x => new { x.Source, x.Id });
            entity.Property(x => x.Source).HasMaxLength(512);
            entity.Property(x => x.Id).HasMaxLength(128);
            entity.Property(x => x.Type).HasMaxLength(256);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.LastException).HasMaxLength(2048);
        });
    }
}

/// <summary>Décrit les états persistants du cycle de traitement d’une livraison.</summary>
internal enum InboxStatus
{
    /// <summary>La livraison possède un lease de traitement actif.</summary>
    Processing,
    /// <summary>Tous les effets métier ont été validés.</summary>
    Completed,
    /// <summary>Une nouvelle tentative est autorisée après une erreur transitoire.</summary>
    RetryScheduled,
    /// <summary>Le nombre maximal de tentatives est dépassé.</summary>
    DeadLettered
}

/// <summary>Modèle de persistance minimal d’une entrée d’inbox idempotente.</summary>
internal sealed class InboxEntry
{
    /// <summary>Obtient ou définit la source CloudEvent, première partie de la clé.</summary>
    public string Source { get; set; } = string.Empty;
    /// <summary>Obtient ou définit l’identifiant CloudEvent, seconde partie de la clé.</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>Obtient ou définit le type CloudEvent reçu.</summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>Obtient ou définit la date de première réception.</summary>
    public DateTimeOffset ReceivedAt { get; set; }
    /// <summary>Obtient ou définit l’état du traitement.</summary>
    public InboxStatus Status { get; set; }
    /// <summary>Obtient ou définit le nombre de tentatives commencées.</summary>
    public int Attempts { get; set; }
    /// <summary>Obtient ou définit la date de début de la dernière tentative.</summary>
    public DateTimeOffset LastAttemptAt { get; set; }
    /// <summary>Obtient ou définit la dernière exception normalisée, sans stack trace sensible.</summary>
    public string? LastException { get; set; }
    /// <summary>Obtient ou définit la date de finalisation réussie.</summary>
    public DateTimeOffset? CompletedAt { get; set; }
}
