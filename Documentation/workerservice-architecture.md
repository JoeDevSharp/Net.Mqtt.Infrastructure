# Architecture recommandée pour un Worker Service MQTT

Ce guide décrit une architecture de référence pour un Worker Service construit avec
`Net.Mqtt.Infrastructure`. L’objectif est de séparer le transport MQTT des règles métier, de
faciliter les tests et d’éviter les dépendances circulaires lorsque le processus consomme également
des bases de données, des API HTTP, du stockage objet ou d’autres ressources externes.

## Principes

1. `Program.cs` est exclusivement la racine de composition. Il configure l’hôte et le conteneur,
   mais ne contient aucune logique métier.
2. Un `BackgroundService` adapte les messages MQTT aux cas d’utilisation. Il n’implémente ni règles
   métier ni accès direct aux bases de données ou aux API externes.
3. La couche Application définit les cas d’utilisation et les ports (`IRepository`, gateways et
   services externes).
4. Infrastructure implémente ces ports. Application ne référence jamais Infrastructure.
5. Domain ne connaît ni MQTT, ni CloudEvents, ni EF Core, ni HTTP, ni le conteneur de dépendances.
6. Les Event Entities sont des contrats de transport. Elles ne doivent pas servir d’entités de
   domaine ou de modèles de persistance.
7. Un message MQTT n’est acquitté qu’après l’achèvement de tous ses effets métier.
8. Toute opération longue reçoit et propage un `CancellationToken`.

## Convention de nommage des solutions

Toute solution déployable doit respecter le modèle suivant :

```text
{Company}.{Domain}.{WorkloadType}.{Capability}.sln
```

| Segment | Description | Exemple |
|---|---|---|
| `Company` | Organisation, produit ou écosystème propriétaire | `Mint` |
| `Domain` | Domaine fonctionnel ou bounded context | `Sales`, `Billing`, `Identity` |
| `WorkloadType` | Nature opérationnelle du composant déployable | `WorkerService`, `WebService` |
| `Capability` | Capacité fonctionnelle stable fournie par le composant | `OrderProcessing`, `InvoiceProcessing` |

### Types de workload (`WorkloadType`)

La convention utilise le terme `WorkloadType` plutôt que `ServiceType`, car la liste inclut aussi
des applications web, des jobs et des fonctions qui ne sont pas toujours des services résidents.

| WorkloadType | Description | Responsabilité principale | Ne doit pas être utilisé pour |
|---|---|---|---|
| `WorkerService` | Processus de longue durée sans interface utilisateur, administré par un hôte | Consommer MQTT, des files ou des streams ; exécuter des traitements continus ; réagir aux événements ; maintenir des listeners et Hosted Services | Exposer une API HTTP comme responsabilité principale, exécuter un lot fini ou contenir une interface web |
| `WebService` | Service réseau exposant des contrats HTTP/gRPC pour la communication inter-systèmes | Fournir des API, valider les requêtes, authentifier/autoriser, exécuter des cas d’utilisation et retourner des réponses ; peut adapter HTTP à MQTT request/reply | Rendre une application destinée aux utilisateurs finaux ou exécuter principalement des consommateurs continus |
| `DeviceService` | Logiciel déployé sur un équipement IoT ou exclusivement associé à un type d’appareil | Piloter le matériel, acquérir la télémétrie, appliquer une logique locale, publier l’état et recevoir les commandes de l’appareil | Représenter une instance physique précise dans le nom de solution ou concentrer les règles métier de l’entreprise |
| `GatewayService` | Frontière entre réseaux, protocoles ou zones de sécurité | Traduire les protocoles, normaliser les messages, agréger les connexions, appliquer l’authentification technique, le rate limiting et le routage | Devenir propriétaire de règles métier ou d’une persistance de domaine appartenant aux services internes |
| `WebApplication` | Application destinée aux utilisateurs, généralement une UI web et éventuellement un backend/BFF | Rendre ou servir l’interface, gérer la navigation/session utilisateur et orchestrer les API pour l’expérience utilisateur | Traitement batch, intégration d’appareils ou API exclusivement machine-à-machine sans UI |
| `BackgroundJob` | Exécution finie déclenchée par un horaire, un scheduler ou une opération administrative | Imports, exports, rapprochements, nettoyage, maintenance et traitement batch ; se termine une fois le travail achevé | Listeners permanents, API toujours actives ou fonctions très courtes administrées par une plateforme serverless |
| `Function` | Unité serverless activée à la demande par un événement ou un appel | Exécuter une opération courte, stateless et scalable dont le lifecycle est contrôlé par la plateforme | Processus résidents, état en mémoire entre appels ou tâches dépassant les limites d’exécution de la plateforme |

`WebService` et `WebApplication` ne sont pas synonymes : le premier expose principalement des
capacités à d’autres systèmes ; le second fournit une expérience utilisateur. Un `WorkerService`
reste actif pour consommer du travail, tandis qu’un `BackgroundJob` possède un début et une fin
définis. Une `Function` est un workload dont le lifecycle et la mise à l’échelle sont contrôlés par
une plateforme serverless.

Règles de la convention :

- utiliser `PascalCase` et séparer les concepts par des points ;
- choisir un `WorkloadType` du tableau, sans abréviation locale ;
- nommer une capacité métier, non une technologie (`OrderProcessing`, pas `MqttConsumer`) ;
- ne pas inclure version, environnement, région, hostname ou identité d’instance ;
- nommer le type d’appareil, jamais l’appareil physique concret ;
- réserver les suffixes de couche (`.Application`, `.Domain`, etc.) aux projets, pas à `Capability`.

Exemples :

```text
Mint.Sales.WorkerService.OrderProcessing.sln
Mint.Retail.DeviceService.PointOfSale.sln
Mint.Integration.GatewayService.PartnerRouting.sln
Mint.Identity.WebService.Authentication.sln
Mint.Operations.WebApplication.ControlCenter.sln
Mint.Billing.BackgroundJob.InvoiceReconciliation.sln
Mint.Notifications.Function.SendEmail.sln
```

## Structure de la solution

Pour une application moyenne ou grande, il est recommandé de séparer les projets, et pas seulement
les dossiers.

L’exemple utilise `Mint.Sales.WorkerService.OrderProcessing` : il représente un service applicatif
qui traite des commandes. Il identifie l’entreprise, le domaine, le type de workload et la capacité
sans coupler le nom à MQTT, .NET, une version ou un environnement.

```text
Mint.Sales.WorkerService.OrderProcessing.sln
│
├─ src/
│  ├─ Mint.Sales.WorkerService.OrderProcessing.Worker/
│  │  ├─ Program.cs
│  │  ├─ Configuration/
│  │  │  └─ MqttSettings.cs
│  │  ├─ HostedServices/
│  │  │  ├─ OrderConsumerWorker.cs
│  │  │  └─ CommandResponderWorker.cs
│  │  └─ DependencyInjection.cs
│  │
│  ├─ Mint.Sales.WorkerService.OrderProcessing.Application/
│  │  ├─ Abstractions/
│  │  │  ├─ Persistence/
│  │  │  │  └─ IOrderRepository.cs
│  │  │  └─ Services/
│  │  │     └─ ICustomerCatalogService.cs
│  │  ├─ Orders/
│  │  │  ├─ ProcessOrderCommand.cs
│  │  │  └─ ProcessOrderHandler.cs
│  │  └─ DependencyInjection.cs
│  │
│  ├─ Mint.Sales.WorkerService.OrderProcessing.Domain/
│  │  ├─ Entities/
│  │  ├─ ValueObjects/
│  │  ├─ Services/
│  │  └─ Exceptions/
│  │
│  ├─ Mint.Sales.WorkerService.OrderProcessing.Messaging/
│  │  ├─ EventEntities/
│  │  │  ├─ OrderSubmitted.cs
│  │  │  └─ OrderProcessed.cs
│  │  ├─ Mqtt/
│  │  │  └─ OrderProcessingMqttContext.cs
│  │  ├─ Schemas/
│  │  └─ DependencyInjection.cs
│  │
│  └─ Mint.Sales.WorkerService.OrderProcessing.Infrastructure/
│     ├─ Persistence/
│     │  ├─ OrdersDbContext.cs
│     │  └─ OrderRepository.cs
│     ├─ ExternalServices/
│     │  └─ CustomerCatalogService.cs
│     └─ DependencyInjection.cs
│
└─ tests/
   ├─ Mint.Sales.WorkerService.OrderProcessing.Domain.Tests/
   ├─ Mint.Sales.WorkerService.OrderProcessing.Application.Tests/
   └─ Mint.Sales.WorkerService.OrderProcessing.Worker.IntegrationTests/
```

Dans une petite solution, ces divisions peuvent être des dossiers d’un projet unique. Les règles de
dépendance doivent néanmoins être conservées.

Une implémentation exécutable et installable avec `dotnet new` est disponible dans
[`Templates/Mint.Sales.WorkerService.OrderProcessing`](../Templates/Mint.Sales.WorkerService.OrderProcessing/README.md).
Elle sert de point de départ industrialisé et conserve les directions de dépendances décrites ici.

### Dossiers logiques de la solution

Les chemins physiques `src/` et `tests/` restent simples. Dans le fichier `.sln`, les projets
doivent également être classés dans des dossiers de solution qui expriment leur rôle architectural :

```text
src
├─ Core
│  ├─ Mint.Sales.WorkerService.OrderProcessing.Domain.csproj
│  └─ Mint.Sales.WorkerService.OrderProcessing.Application.csproj
├─ Adapters
│  ├─ Mint.Sales.WorkerService.OrderProcessing.Messaging.csproj
│  └─ Mint.Sales.WorkerService.OrderProcessing.Infrastructure.csproj
└─ Host
   └─ Mint.Sales.WorkerService.OrderProcessing.Worker.csproj

tests
├─ Unit
│  ├─ Mint.Sales.WorkerService.OrderProcessing.Domain.Tests.csproj
│  └─ Mint.Sales.WorkerService.OrderProcessing.Application.Tests.csproj
└─ Integration
   └─ Mint.Sales.WorkerService.OrderProcessing.Worker.IntegrationTests.csproj
```

| Dossier de solution | Contenu autorisé |
|---|---|
| `src/Core` | Domain et Application ; règles métier, cas d’utilisation et ports |
| `src/Adapters` | Messaging et Infrastructure ; adaptateurs MQTT, EF Core, HTTP et autres ressources externes |
| `src/Host` | Projet exécutable, composition, configuration et Hosted Services |
| `tests/Unit` | Tests isolés de Domain et Application |
| `tests/Integration` | Tests de transport, persistance, composition et interactions entre adaptateurs |

Ces dossiers sont une organisation logique de l’IDE : ils ne constituent pas des assemblies et ne
modifient pas la direction des dépendances. Ne pas créer un projet `Core`, `Adapters` ou `Host`
uniquement pour reproduire cette arborescence.

## Direction des dépendances

```text
                         ┌──────────────────┐
                         │      Worker      │  composition et HostedServices
                         └───────┬──────────┘
                                 │
                 ┌───────────────┼────────────────┐
                 ▼               ▼                ▼
          Application        Messaging       Infrastructure
                 │                                │
                 ▼                                │
              Domain ◄────────────────────────────┘

Application ──X──> Infrastructure
Domain      ──X──> Application, Messaging, Infrastructure ou Worker
Messaging   ──X──> Application, Domain et Infrastructure
```

Références recommandées :

| Projet | Peut référencer |
|---|---|
| Domain | Aucun projet applicatif |
| Application | Domain |
| Messaging | `Net.Mqtt.Infrastructure` |
| Infrastructure | Application et Domain |
| Worker | Application, Messaging et Infrastructure |

L’exécutable Worker est la seule couche qui connaît toutes les implémentations. Cela évite qu’un
repository doive résoudre un Worker, qu’Application dépende d’EF Core ou que Domain finisse par
dépendre d’une Event Entity.

## Point d’entrée : `Program.cs`

Utiliser `Host.CreateApplicationBuilder(args)`. Ce builder combine configuration, logging,
injection de dépendances, lifecycle et Hosted Services. `Program.cs` doit rester court et exprimer
la composition, non les détails de chaque enregistrement :

```csharp
using Mint.Sales.WorkerService.OrderProcessing.Application;
using Mint.Sales.WorkerService.OrderProcessing.Infrastructure;
using Mint.Sales.WorkerService.OrderProcessing.Messaging;
using Mint.Sales.WorkerService.OrderProcessing.Worker.HostedServices;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOrderProcessingApplication()
    .AddOrderProcessingInfrastructure(builder.Configuration)
    .AddOrderProcessingMessaging(builder.Configuration);

builder.Services.AddHostedService<OrderConsumerWorker>();
builder.Services.AddHostedService<CommandResponderWorker>();

await builder.Build().RunAsync();
```

À éviter dans `Program.cs` :

- construire manuellement un `ServiceProvider` ;
- résoudre des services avant `Build()` ;
- créer manuellement des connexions MQTT, `DbContext` ou `HttpClient` ;
- exécuter de la logique fonctionnelle ;
- enregistrer un à un et de manière répétitive tous les cas d’utilisation.

Regrouper les enregistrements par couche au moyen d’extensions `IServiceCollection`.

## Configuration fortement typée

Les adresses, identités et timeouts doivent provenir de la configuration. Ils ne doivent pas être
dispersés comme constantes dans les Workers :

```csharp
public sealed class MqttSettings
{
    public const string SectionName = "Mqtt";

    public required string Host { get; init; }
    public int Port { get; init; } = 1883;
    public required string ClientId { get; init; }
    public required string BaseTopic { get; init; }
    public required string ModuleIdentity { get; init; }
    public required string ServiceIdentity { get; init; }
    public required string CloudEventSource { get; init; }
}
```

Enregistrer et valider les options au démarrage :

```csharp
services.AddOptions<MqttSettings>()
    .Bind(configuration.GetRequiredSection(MqttSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

L’application doit échouer au démarrage si une identité ou une URI manque. Cela est préférable à
un échec lors du traitement du premier message.

## Couche Messaging et Application Builder

La configuration de la bibliothèque doit être encapsulée dans le projet Messaging. Le Worker
appelle une extension et n’a pas besoin de connaître chaque schema :

```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddOrderProcessingMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration
            .GetRequiredSection(MqttSettings.SectionName)
            .Get<MqttSettings>()
            ?? throw new InvalidOperationException("Configuration MQTT absente.");

        services.AddMqttReactiveOrm<OrderProcessingMqttContext>(mqtt =>
        {
            mqtt.ConnectTo(settings.Host, settings.Port)
                .IdentifyAs(settings.ClientId)
                .WithBaseTopic(settings.BaseTopic)
                .ForModule(settings.ModuleIdentity)
                .ForService(settings.ServiceIdentity)
                .WithCloudEventSource(settings.CloudEventSource)
                .UseUnavailableLastWill()
                .UseProductionDefaults();

            mqtt.UseEventEntities(RegisterEventEntities);
            mqtt.UseSchemas(RegisterSchemas);
        });

        return services;
    }

    private static void RegisterEventEntities(EventEntityRegistryBuilder entities)
    {
        entities.Add<OrderSubmitted>();
        entities.Add<OrderProcessed>();
    }

    private static void RegisterSchemas(MqttSchemaBuilder schemas)
    {
        schemas.AddInline(
            "urn:schema:sales:order-processing:order-submitted:v1",
            OrderProcessingSchemas.OrderSubmittedV1,
            "1.0.0");
    }
}
```

En développement ou pour les tests, `UseInMemoryTransport()` peut être sélectionné. Ne pas mélanger
ce choix avec les cas d’utilisation : il relève de la composition/configuration.

## Event Entities

Une Event Entity représente le payload transporté. Elle doit être immuable, ou traitée comme telle,
et déclarer ses attributs obligatoires :

```csharp
[EventType("com.mint.sales.order-processing.order-submitted.v1")]
[DataSchema("urn:schema:sales:order-processing:order-submitted:v1")]
[EventVersion("1.0.0")]
[MaximumDataSize(16 * 1024)]
public sealed record OrderSubmitted
{
    public required string OrderId { get; init; }
    public required string CustomerId { get; init; }
    public decimal Total { get; init; }
    public DateTimeOffset SubmittedAt { get; init; }
}
```

Ne pas utiliser l’Event Entity comme entité EF Core. La convertir en commande d’Application ou en
entité/Value Object de Domain. Cette traduction protège le domaine des évolutions du contrat de
transport.

## Contexte MQTT

Le contexte déclare uniquement les topics et les relations request/reply :

```csharp
public sealed class OrderProcessingMqttContext(MqttContextDependencies dependencies)
    : MqttOrmContext(dependencies)
{
    [MqttTopic(
        PublishTopic = "orders/submitted",
        SubscribeFilter = "orders/submitted",
        QoS = MqttQoS.AtLeastOnce)]
    public TopicSet<OrderSubmitted> SubmittedOrders => Set<OrderSubmitted>();

    [MqttTopic(
        PublishTopic = "orders/processed",
        SubscribeFilter = "orders/processed",
        QoS = MqttQoS.AtLeastOnce)]
    public TopicSet<OrderProcessed> ProcessedOrders => Set<OrderProcessed>();
}
```

Les topics sont relatifs à :

```text
{BaseTopic}/moduls/{ModuleIdentity}/services/{ServiceIdentity}
```

Utiliser `../` uniquement lorsque le contrat système exige une publication à un niveau supérieur de
la hiérarchie. La bibliothèque interdit toute remontée au-dessus de `WithBaseTopic`.

## Couche Application

Application exprime une opération fonctionnelle sans connaître MQTT :

```csharp
public sealed record ProcessOrderCommand(
    string OrderId,
    string CustomerId,
    decimal Total,
    DateTimeOffset SubmittedAt);

public interface IProcessOrder
{
    Task<ProcessOrderResult> ExecuteAsync(
        ProcessOrderCommand command,
        CancellationToken cancellationToken);
}

public sealed class ProcessOrderHandler(
    IOrderRepository orders,
    ICustomerCatalogService customers) : IProcessOrder
{
    public async Task<ProcessOrderResult> ExecuteAsync(
        ProcessOrderCommand command,
        CancellationToken cancellationToken)
    {
        var customer = await customers.GetAsync(command.CustomerId, cancellationToken);
        var decision = OrderPolicy.Evaluate(customer, command.Total);
        await orders.SaveDecisionAsync(decision, cancellationToken);
        return new(decision.Status, decision.Action);
    }
}
```

Les interfaces des ressources externes appartiennent à Application, car Application définit ses
besoins. Infrastructure décide comment les satisfaire.

## Repositories et services externes

Utiliser un repository pour la persistance et les requêtes sur les agrégats propres :

```csharp
public interface IOrderRepository
{
    Task<Order?> GetAsync(string id, CancellationToken cancellationToken);
    Task SaveDecisionAsync(OrderDecision decision, CancellationToken cancellationToken);
}
```

Utiliser un service/gateway pour les API ou capacités externes :

```csharp
public interface ICustomerCatalogService
{
    Task<CustomerInfo> GetAsync(string customerId, CancellationToken cancellationToken);
}
```

Leurs implémentations résident dans Infrastructure :

```csharp
internal sealed class OrderRepository(OrdersDbContext db)
    : IOrderRepository
{
    public Task<Order?> GetAsync(string id, CancellationToken cancellationToken) =>
        db.Orders.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task SaveDecisionAsync(
        OrderDecision decision,
        CancellationToken cancellationToken)
    {
        db.Decisions.Add(OrderDecisionRow.FromDomain(decision));
        await db.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class CustomerCatalogService(HttpClient httpClient)
    : ICustomerCatalogService
{
    public async Task<CustomerInfo> GetAsync(
        string customerId,
        CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<CustomerInfo>(
            $"customers/{Uri.EscapeDataString(customerId)}",
            cancellationToken)
        ?? throw new InvalidOperationException($"Le client '{customerId}' est introuvable.");
}
```

Enregistrement d’Infrastructure :

```csharp
public static IServiceCollection AddOrderProcessingInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddDbContext<OrdersDbContext>(options =>
        options.UseSqlServer(configuration.GetConnectionString("Orders")));

    services.AddScoped<IOrderRepository, OrderRepository>();
    services.AddHttpClient<ICustomerCatalogService, CustomerCatalogService>(client =>
        client.BaseAddress = new Uri(configuration["CustomerCatalog:BaseUrl"]!));

    return services;
}
```

Ne pas injecter `HttpClient` sans `IHttpClientFactory`, ne pas créer une connexion à la base de
données par message et ne pas autoriser un repository à publier sur MQTT. Le cas d’utilisation
coordonne les ports.

## Hosted Services comme adaptateurs

Un Hosted Service est singleton. Il ne doit pas recevoir directement des services scoped tels que
`DbContext` ou un repository scoped. Il doit créer un scope par message ou unité de travail :

```csharp
public sealed class OrderConsumerWorker(
    OrderProcessingMqttContext mqtt,
    IServiceScopeFactory scopeFactory,
    ILogger<OrderConsumerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in mqtt.SubmittedOrders.ReadAllAsync(
            new SubscriptionOptions { Capacity = 32 },
            stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var useCase = scope.ServiceProvider.GetRequiredService<IProcessOrder>();

                var result = await useCase.ExecuteAsync(
                    new ProcessOrderCommand(
                        message.Data.OrderId,
                        message.Data.CustomerId,
                        message.Data.Total,
                        message.Data.SubmittedAt),
                    stoppingToken);

                await mqtt.ProcessedOrders.PublishAsync(
                    new OrderProcessed
                    {
                        OrderId = message.Data.OrderId,
                        Status = result.Status,
                        Action = result.Action
                    },
                    new CloudEventPublishOptions
                    {
                        Context = new CloudEventPublishContext
                        {
                            Extensions = new CloudEventExtensions
                            {
                                CorrelationId = message.CloudEvent.Extensions.CorrelationId,
                                CausationId = message.CloudEvent.Id,
                                TraceParent = message.CloudEvent.Extensions.TraceParent,
                                TraceState = message.CloudEvent.Extensions.TraceState
                            }
                        }
                    },
                    stoppingToken);

                await message.AcknowledgeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Échec du traitement de la commande pour le CloudEvent {Source}/{Id}",
                    message.CloudEvent.Source,
                    message.CloudEvent.Id);

                // Pas d’ACK : QoS/session peut provoquer une nouvelle livraison.
                // La politique retry/DLQ doit classifier l’erreur.
            }
        }
    }
}
```

Règles du consommateur :

- utiliser `ReadAllAsync`, non `IObservable<T>`, lorsqu’ACK et backpressure sont nécessaires ;
- configurer une capacité bornée ;
- créer un scope par unité de travail ;
- propager corrélation, causation et tracing ;
- publier le résultat avant l’ACK ;
- ne pas acquitter un message dont l’effet métier a échoué ;
- distinguer les erreurs non rejouables des défaillances transitoires ;
- ne pas traiter `CancellationToken` comme une erreur fonctionnelle.

## Traitement parallèle

La boucle précédente est séquentielle et offre le comportement le plus prévisible. Si le cas exige
du parallélisme :

- limiter la concurrence avec `Parallel.ForEachAsync`, `Channel<T>` ou `SemaphoreSlim` ;
- conserver un scope et un `DbContext` indépendants par message ;
- ne pas partager d’entités EF entre les tâches ;
- préserver l’ordre lorsque le topic représente une séquence ;
- dimensionner ensemble `SubscriptionOptions.Capacity` et MQTT `ReceiveMaximum` ;
- attendre les travaux en cours pendant l’arrêt.

Ne pas utiliser de `Task.Run` non suivi dans le Worker.

## Request/reply

Pour les requêtes MQTT corrélées, utiliser `MqttRequestSet<TRequest,TResponse>`. La bibliothèque
partage la souscription aux réponses, attend le `SUBACK`, génère `correlationid` et gère timeout et
ACK :

```csharp
public MqttRequestSet<ReadOrder, OrderResponse> OrderRequest =>
    Request<ReadOrder, OrderResponse>(
        nameof(ReadRequests),
        nameof(ReadResponses));

var response = await mqtt.OrderRequest.SendAsync(
    new ReadOrder { OrderId = id },
    new MqttRequestOptions { Timeout = TimeSpan.FromSeconds(10) },
    cancellationToken);
```

Pour répondre depuis un Hosted Service :

```csharp
protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
    mqtt.OrderRequest.HandleAsync(
        async (request, cancellationToken) =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var useCase = scope.ServiceProvider.GetRequiredService<IReadOrder>();
            var result = await useCase.ExecuteAsync(request.Data.OrderId, cancellationToken);
            return new OrderResponse { Status = result.Status };
        },
        stoppingToken);
```

Ne pas créer un énumérateur MQTT par requête HTTP et ne pas corréler les réponses en comparant leur
contenu. L’identité correcte est `correlationid`.

## Idempotence et unité de travail

QoS 1 garantit au moins une livraison, pas exactement un effet. Un crash peut survenir après
l’écriture en base et avant l’ACK. Par conséquent :

- utiliser `source + id` de CloudEvents comme clé idempotente ;
- stocker cette clé dans une inbox avec un index unique ;
- coordonner inbox et changements métier dans la même transaction lorsqu’ils partagent la base ;
- marquer `COMPLETED` avant l’ACK ;
- traiter une nouvelle livraison déjà terminée comme un succès sans répéter le cas d’utilisation ;
- appliquer retry uniquement aux erreurs transitoires ;
- envoyer les messages invalides ou épuisés vers dead letter/quarantine.

Sans inbox transactionnelle, le Worker doit supposer que le cas d’utilisation peut être exécuté
plusieurs fois et concevoir des opérations idempotentes.

## Lifetimes d’injection de dépendances

| Service | Lifetime recommandé | Motif |
|---|---|---|
| `OrderProcessingMqttContext` / `IMqttBus` | Singleton, administré par la bibliothèque | Une connexion et un modèle de topics par processus |
| `BackgroundService` | Singleton | Contrat du Generic Host |
| Cas d’utilisation sans état | Scoped ou transient | Unité de travail explicite |
| `DbContext` et repositories EF | Scoped | Ils ne sont pas thread-safe |
| Typed `HttpClient` | Transient administré par `IHttpClientFactory` | Pooling correct des handlers |
| Services de domaine purs | Singleton ou transient | Selon leur état et leurs dépendances |

Ne jamais injecter directement un scoped dans un Hosted Service singleton. Utiliser
`IServiceScopeFactory`.

## Observabilité

Chaque log de traitement devrait inclure :

- `source` et `id` du CloudEvent ;
- `type`;
- `correlationid` et `causationid` ;
- le topic MQTT ;
- le numéro de tentative si inbox/retry existe ;
- la durée du cas d’utilisation ;
- le résultat (`completed`, `retry`, `dead-lettered`).

Ne pas journaliser les payloads complets s’ils peuvent contenir des données sensibles. Propager
`traceparent` et `tracestate` lors de la publication d’événements dérivés.

## Arrêt et résilience

- respecter `stoppingToken` dans les lectures, repositories, appels HTTP et publications ;
- laisser le Host appeler le service MQTT de lifecycle ;
- ne pas appeler manuellement `ConnectAsync` depuis chaque Worker ;
- utiliser `UseProductionDefaults()` pour la session persistante, la reconnexion et le Last Will ;
- appliquer des timeouts à toute dépendance externe ;
- placer les politiques retry dans Infrastructure, sans boucle infinie dans le cas d’utilisation ;
- publier le Last Will dans le namespace gouverné par la bibliothèque ;
- vérifier la readiness du service avant d’accepter du trafic dépendant de MQTT.

## Tests

### Domain

Tester les règles et Value Objects sans DI, MQTT ni base de données.

### Application

Tester les cas d’utilisation avec des fakes de repositories et services externes. Vérifier les
résultats, l’annulation et la classification des erreurs.

### Messaging et intégration

Utiliser `UseInMemoryTransport()` pour valider Event Entities, schemas, topics, request/reply,
corrélation et ACK sans Mosquitto :

```csharp
services.AddMqttReactiveOrm<TestMqttContext>(mqtt =>
{
    mqtt.UseInMemoryTransport()
        .WithBaseTopic("tests/v1")
        .ForModule("orders")
        .ForService("worker")
        .WithCloudEventSource("urn:tests:order-processing-worker");

    mqtt.UseEventEntities(RegisterTestEntities);
    mqtt.UseSchemas(RegisterTestSchemas);
});
```

Ajouter des tests avec un broker réel pour la session persistante, QoS, la reconnexion, les ACL, le
Last Will et le comportement pendant les coupures réseau.

## Liste de contrôle

- [ ] `Program.cs` compose uniquement l’application.
- [ ] Domain ne référence ni MQTT, ni EF Core, ni HTTP.
- [ ] Application définit les interfaces des ressources externes.
- [ ] Infrastructure implémente les repositories et services externes.
- [ ] Messaging contient Event Entities, schemas et `MqttOrmContext`.
- [ ] Les Hosted Services adaptent MQTT aux cas d’utilisation.
- [ ] Chaque message crée son propre scope lorsqu’il utilise des services scoped.
- [ ] `CancellationToken` est propagé.
- [ ] Une capacité bornée et une concurrence limitée sont utilisées.
- [ ] Les résultats sont publiés avant l’ACK.
- [ ] Corrélation, causation et tracing sont propagés.
- [ ] Les effets sont idempotents grâce à `source + id`.
- [ ] Request/reply utilise `MqttRequestSet`, pas une souscription par requête.
- [ ] Des tests in-memory et avec broker réel existent.
