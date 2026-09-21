# Documentation du template Worker Service MQTT

Ce portail documente l’implémentation de référence
`Mint.Sales.WorkerService.OrderProcessing`. Il complète l’architecture générale en expliquant le
rôle de chaque projet, la composition du conteneur, les adaptateurs et le cycle complet d’une
livraison MQTT.

## Parcours recommandé

1. [Vue d’ensemble et dépendances](01-overview.md)
2. [Composition et cycle d’injection de dépendances](02-dependency-injection.md)
3. [Projet Domain](03-domain.md)
4. [Projet Application](04-application.md)
5. [Projet Messaging](05-messaging.md)
6. [Projet Infrastructure et adaptateurs](06-infrastructure.md)
7. [Projet Worker et cycle d’un message](07-worker-runtime.md)
8. [Inbox, idempotence et unité de travail](08-inbox-idempotence.md)
9. [Configuration et environnements](09-configuration.md)
10. [Stratégie de tests](10-testing.md)
11. [Industrialisation avec `dotnet new`](11-template-usage.md)
12. [Conteneurisation avec Docker et Compose](12-containers.md)

## Carte rapide des projets

| Projet | Rôle | Références autorisées |
|---|---|---|
| `Domain` | Entités, Value Objects et règles métier pures | Aucune couche applicative |
| `Application` | Cas d’utilisation et ports nécessaires | `Domain` |
| `Messaging` | Event Entities, schemas, topics et contexte MQTT | `Net.Mqtt.Infrastructure` |
| `Infrastructure` | EF Core, inbox, repositories et services HTTP | `Application`, `Domain` |
| `Worker` | Composition et adaptation MQTT vers Application | `Application`, `Messaging`, `Infrastructure` |
| `*.Tests` | Vérification par niveau | Seulement les projets testés et leurs dépendances nécessaires |

## Flux complet du template

```mermaid
flowchart TD
    Start([Démarrage du processus])

    subgraph WorkerProject["Mint.Sales.WorkerService.OrderProcessing.Worker"]
        direction TB
        Program["Program.cs<br/>racine de composition"]
        Workers["Hosted Services singleton"]
        Consumer[OrderConsumerWorker]
        Scope["Scope DI par livraison"]
        MapCommand["OrderSubmitted<br/>vers ProcessOrderCommand"]
        AckDuplicate["ACK sans répéter<br/>les effets métier"]
        NoAck["Pas d’ACK<br/>lease encore actif"]
        Ack["ACK de la livraison"]
        NoAckFailure["Pas d’ACK<br/>redelivery possible"]
        Responder[CommandResponderWorker]
        ReplyScope["Scope DI par requête"]
        AckRequest["ACK de la requête réussie"]
    end

    subgraph ApplicationProject["Mint.Sales.WorkerService.OrderProcessing.Application"]
        direction TB
        AddApp[AddOrderProcessingApplication]
        UseCases["Handlers scoped<br/>IProcessOrder / IReadOrder"]
        ProcessOrder["IProcessOrder.ExecuteAsync"]
        ReadOrder["IReadOrder.ExecuteAsync"]
        RepositoryPort[IOrderRepository]
        CustomerPort[ICustomerCatalogService]
        InboxPort[IInbox]
        UnitOfWorkPort[IOrderUnitOfWork]
        Decision[ProcessOrderResult]
    end

    subgraph DomainProject["Mint.Sales.WorkerService.OrderProcessing.Domain"]
        direction TB
        OrderAggregate["Agrégat Order"]
        Policy[OrderPolicy.Evaluate]
        DomainDecision[OrderDecision]
    end

    subgraph MessagingProject["Mint.Sales.WorkerService.OrderProcessing.Messaging"]
        direction TB
        AddMessaging[AddOrderProcessingMessaging]
        ValidateConfig{"Configuration MQTT valide ?"}
        EventEntities["Event Entities<br/>et JSON Schemas"]
        MqttContext["OrderProcessingMqttContext<br/>TopicSet et RequestSet"]
        MqttLifecycle["Lifecycle MQTT singleton<br/>session et reconnexion"]
        Publish["Publier OrderProcessed<br/>corrélation, causation et tracing"]
        Response["Publier OrderResponse<br/>avec correlationid"]
    end

    subgraph InfrastructureProject["Mint.Sales.WorkerService.OrderProcessing.Infrastructure"]
        direction TB
        AddInfra[AddOrderProcessingInfrastructure]
        Repository["OrderRepository<br/>implémente IOrderRepository"]
        Inbox["EfInbox<br/>implémente IInbox"]
        InboxDecision{"Décision inbox"}
        SaveLease["Persister PROCESSING<br/>et la tentative"]
        Complete["Marquer COMPLETED"]
        Failure["Enregistrer l’exception<br/>normalisée"]
        FailureState{"Nombre maximal<br/>de tentatives ?"}
        Retry[RETRY_SCHEDULED]
        Dead[DEAD_LETTERED]
        UnitOfWork["OrderUnitOfWork<br/>SaveChangesAsync"]
        HttpClient["CustomerCatalogService<br/>typed HttpClient"]
        Db[("OrdersDbContext<br/>Orders + Inbox")]
    end

    subgraph ExternalSystems["Systèmes externes"]
        direction TB
        Broker[(Broker MQTT)]
        CustomerApi[(Catalogue client HTTP)]
        StartupFailure([Échec explicite au démarrage])
    end

    Start --> Program
    Program --> AddApp
    Program --> AddInfra
    Program --> AddMessaging
    AddApp --> UseCases
    AddInfra --> Repository
    AddInfra --> Inbox
    AddInfra --> UnitOfWork
    AddInfra --> HttpClient
    AddMessaging --> ValidateConfig
    AddMessaging --> EventEntities
    AddMessaging --> MqttContext
    ValidateConfig -- Non --> StartupFailure
    ValidateConfig -- Oui --> MqttLifecycle
    MqttLifecycle --> Workers
    Workers --> Consumer
    Workers --> Responder

    Broker -->|OrderSubmitted| MqttContext
    MqttContext --> Consumer
    Consumer --> Scope
    Scope --> InboxPort
    InboxPort --> Inbox
    Inbox --> InboxDecision
    InboxDecision -- AlreadyCompleted --> AckDuplicate
    InboxDecision -- AlreadyProcessing --> NoAck
    InboxDecision -- Started --> SaveLease
    SaveLease --> Db
    SaveLease --> MapCommand
    MapCommand --> ProcessOrder
    ProcessOrder --> RepositoryPort
    RepositoryPort --> Repository
    Repository --> Db
    ProcessOrder --> CustomerPort
    CustomerPort --> HttpClient
    HttpClient --> CustomerApi
    ProcessOrder --> OrderAggregate
    OrderAggregate --> Policy
    Policy --> DomainDecision
    DomainDecision --> Decision
    Decision --> Publish
    Publish --> MqttContext
    MqttContext --> Broker
    Publish --> InboxPort
    InboxPort --> Complete
    Complete --> UnitOfWorkPort
    UnitOfWorkPort --> UnitOfWork
    UnitOfWork --> Db
    UnitOfWork --> Ack
    Ack --> Broker

    ProcessOrder -. exception .-> Failure
    HttpClient -. exception .-> Failure
    Publish -. exception .-> Failure
    UnitOfWork -. exception .-> Failure
    Failure --> FailureState
    FailureState -- Non --> Retry
    FailureState -- Oui --> Dead
    Retry --> Db
    Dead --> Db
    Retry --> NoAckFailure
    Dead --> NoAckFailure

    Broker -->|ReadOrder + correlationid| MqttContext
    MqttContext --> Responder
    Responder --> ReplyScope
    ReplyScope --> ReadOrder
    ReadOrder --> RepositoryPort
    ReadOrder --> Response
    Response --> MqttContext
    MqttContext -->|réponse corrélée| Broker
    Response --> AckRequest

    classDef host fill:#dbeafe,stroke:#2563eb,color:#172554;
    classDef application fill:#fef3c7,stroke:#d97706,color:#451a03;
    classDef domain fill:#dcfce7,stroke:#16a34a,color:#052e16;
    classDef infrastructure fill:#f3e8ff,stroke:#9333ea,color:#3b0764;
    classDef messaging fill:#cffafe,stroke:#0891b2,color:#083344;
    classDef external fill:#f1f5f9,stroke:#475569,color:#0f172a;
    classDef failure fill:#fee2e2,stroke:#dc2626,color:#450a0a;

    class Program,Workers,Consumer,Responder,Scope,ReplyScope,MapCommand,Ack,AckDuplicate,AckRequest host;
    class AddApp,UseCases,ProcessOrder,ReadOrder,RepositoryPort,CustomerPort,InboxPort,UnitOfWorkPort,Decision application;
    class OrderAggregate,Policy,DomainDecision domain;
    class AddInfra,HttpClient,Db,Repository,Inbox,SaveLease,Complete,UnitOfWork,Failure infrastructure;
    class AddMessaging,ValidateConfig,EventEntities,MqttContext,MqttLifecycle,Publish,Response messaging;
    class Broker,CustomerApi external;
    class StartupFailure,NoAck,FailureState,Retry,Dead,NoAckFailure failure;

    style WorkerProject fill:#eff6ff,stroke:#2563eb,stroke-width:2px
    style ApplicationProject fill:#fffbeb,stroke:#d97706,stroke-width:2px
    style DomainProject fill:#f0fdf4,stroke:#16a34a,stroke-width:2px
    style MessagingProject fill:#ecfeff,stroke:#0891b2,stroke-width:2px
    style InfrastructureProject fill:#faf5ff,stroke:#9333ea,stroke-width:2px
    style ExternalSystems fill:#f8fafc,stroke:#475569,stroke-width:2px
```

Le flux principal garantit que les effets métier et l’état `COMPLETED` sont validés avant l’ACK.
La publication MQTT ne partage pas la transaction EF Core ; lorsque sa garantie atomique est requise,
compléter ce flux avec l’outbox décrite dans [Inbox, idempotence et unité de travail](08-inbox-idempotence.md).

## Règle de lecture

Les noms `Order`, `Customer` et `Sales` forment un exemple applicatif. Lors de la génération d’un
service, remplacer le vocabulaire métier, mais conserver les frontières, lifetimes et directions de
dépendances décrits dans ces pages.

Voir également [l’architecture Worker Service générale](../../Documentation/workerservice-architecture.md)
et [le README exécutable du template](../Mint.Sales.WorkerService.OrderProcessing/README.md).
