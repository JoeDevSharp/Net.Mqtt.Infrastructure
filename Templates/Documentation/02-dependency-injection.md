# Composition et cycle d’injection de dépendances

[Retour à l’index](index.md)

## Racine de composition

`Program.cs` est le seul endroit qui connaît simultanément Application, Infrastructure, Messaging
et Worker. Il enregistre les couches dans cet ordre :

```csharp
builder.Services
    .AddOrderProcessingApplication()
    .AddOrderProcessingInfrastructure(builder.Configuration)
    .AddOrderProcessingMessaging(builder.Configuration);

builder.Services.AddHostedService<OrderConsumerWorker>();
builder.Services.AddHostedService<CommandResponderWorker>();
```

Chaque extension `DependencyInjection` appartient à la couche qu’elle configure. `Program.cs` ne
doit contenir ni mapping EF, ni schema JSON, ni construction de `HttpClient`, ni logique métier.

## Lifetimes effectifs

| Service | Lifetime | Pourquoi |
|---|---|---|
| `OrderProcessingMqttContext` | Singleton | Une connexion et un modèle de topics par processus |
| Hosted Services | Singleton | Ils sont créés et administrés par le Generic Host |
| `IProcessOrder`, `IReadOrder` | Scoped | Une instance par unité de travail/message |
| `OrdersDbContext` | Scoped | EF Core n’est pas thread-safe |
| `IOrderRepository`, `IOrderUnitOfWork`, `IInbox` | Scoped | Ils partagent le même `OrdersDbContext` |
| `ICustomerCatalogService` | Transient géré | Typed client créé par `IHttpClientFactory` |
| `TimeProvider` | Singleton | Horloge cohérente et remplaçable en tests |

## Cycle au démarrage

1. `Host.CreateApplicationBuilder` charge configuration et logging.
2. Application enregistre les handlers derrière leurs interfaces.
3. Infrastructure enregistre les implémentations des ports et EF Core.
4. Messaging lit `MqttSettings`, enregistre les Event Entities, schemas et le contexte.
5. Les options sont validées avec `ValidateOnStart`.
6. `Build()` fige le conteneur.
7. Le Host démarre le lifecycle MQTT et les Hosted Services.

Une configuration MQTT invalide doit donc arrêter le processus avant sa première livraison.

## Scope par message

Un `BackgroundService` est singleton et ne doit jamais recevoir directement un repository ou un
`DbContext` scoped. `OrderConsumerWorker` injecte `IServiceScopeFactory`, puis crée un scope pour
chaque livraison :

```csharp
await using var scope = scopeFactory.CreateAsyncScope();
var useCase = scope.ServiceProvider.GetRequiredService<IProcessOrder>();
```

Le repository, l’inbox et l’unité de travail résolus depuis ce scope reçoivent la même instance de
`OrdersDbContext`. La disposition du scope libère correctement le contexte, même après exception.

## Règles d’extension

- Ajouter une interface dans Application avant son implémentation Infrastructure.
- Enregistrer un service scoped si son état appartient à un message ou une transaction.
- Ne jamais capturer un scoped dans un singleton.
- Utiliser un typed `HttpClient`, pas `new HttpClient()`.
- Ne jamais appeler `BuildServiceProvider()` pendant les enregistrements.
- Propager le `CancellationToken` fourni par le Host jusqu’à chaque I/O.
