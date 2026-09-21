# Rapport d’architecture — Abstraction et simplification des workloads MQTT

**Projet analysé :** `Net.Mqtt.ReactiveOrm`  
**Template de référence :** `Mint.Sales.WorkerService.OrderProcessing`  
**Date :** 1 septembre 2026  
**Statut :** proposition d’architecture et plan d’industrialisation

## 1. Résumé exécutif

Le template actuel démontre une architecture applicative robuste : séparation Domain/Application,
contrats CloudEvents typés, transport MQTT, scopes par message, repository, unité de travail, inbox,
request/reply, configuration validée, tests et conteneurisation. Cette robustesse produit cependant
une charge cognitive trop élevée pour une équipe dont la responsabilité principale est de développer
une règle métier.

Le problème ne vient pas du découpage en couches. Il vient de la répétition, dans chaque service, de
mécanismes techniques sensibles : lifecycle MQTT, coordination des scopes, ACK, idempotence, retry,
corrélation, tracing et gestion transactionnelle. Laisser ces mécanismes dans les templates entraîne
du code copié, des variantes locales et un risque élevé d’erreurs difficiles à reproduire.

La recommandation est de créer une couche de produit intermédiaire entre les applications et
`Net.Mqtt.Infrastructure` : une famille de packages NuGet nommée provisoirement
`Net.Mqtt.Workloads`. Elle fournirait un modèle applicatif fondé sur la composition de capacités et
des profils de workloads. Les développeurs conserveraient la maîtrise de leurs Event Entities,
topics, règles Domain, cas d’utilisation et ports métier, tandis que la plateforme prendrait en
charge le pipeline fiable.

La priorité doit être le profil `WorkerService`, car il concentre les mécanismes déjà prouvés dans le
template. Les autres profils doivent être construits ensuite à partir des mêmes capacités, sans créer
une hiérarchie rigide de classes de base.

## 2. Contexte et objectif

### 2.1 Situation actuelle

Le template contient cinq projets de production :

| Projet | Responsabilité actuelle |
|---|---|
| `Domain` | Agrégats, décisions et règles métier pures |
| `Application` | Cas d’utilisation et ports |
| `Messaging` | Event Entities, schemas, topics et request/reply |
| `Infrastructure` | EF Core, repository, inbox et service HTTP |
| `Worker` | Composition, consommateurs et responders |

Il fournit également trois projets de tests, Docker/Compose et une documentation détaillée.

### 2.2 Objectif produit

Permettre à une équipe applicative de produire principalement :

- le vocabulaire et les règles Domain ;
- les cas d’utilisation Application ;
- les Event Entities et leurs schemas ;
- les topics et mappings spécifiques ;
- les adaptateurs métier réellement propres au service.

La plateforme doit prendre en charge les mécanismes transversaux et imposer par défaut un
comportement fiable, observable et testable.

### 2.3 Non-objectifs

La nouvelle bibliothèque ne doit pas :

- fusionner Domain, Application et Infrastructure ;
- cacher les contrats CloudEvents ou les topics réellement publiés ;
- transformer toutes les règles métier en configuration fluente ;
- imposer EF Core aux services qui n’en ont pas besoin ;
- créer une classe de base universelle contenant toutes les technologies ;
- promettre une sémantique « exactly once » que MQTT et SQL ne peuvent fournir seuls ;
- concevoir tous les workload types avant d’avoir validé un premier pipeline productif.

## 3. Diagnostic du template

### 3.1 Complexité légitime

Certaines parties doivent rester visibles parce qu’elles appartiennent au service :

- définition de `OrderSubmitted` et `OrderProcessed` ;
- types CloudEvents, URI de schemas et versions ;
- contexte MQTT et topics ;
- `ProcessOrderCommand`, résultat et handler ;
- règles `OrderPolicy` ;
- ports `IOrderRepository` et `ICustomerCatalogService` ;
- mapping entre transport et cas d’utilisation.

### 3.2 Complexité accidentelle ou répétitive

Les éléments suivants ne devraient plus être réimplémentés par chaque équipe :

- boucle `ReadAllAsync` ;
- création et disposition d’un scope par livraison ;
- résolution du handler ;
- appel de l’inbox et interprétation de ses décisions ;
- lease de traitement ;
- ordre publication/commit/ACK ;
- normalisation des exceptions ;
- retry et dead-letter ;
- propagation correlation/causation/tracing ;
- journalisation structurée du contexte CloudEvents ;
- request/reply responder standard ;
- dispatcher outbox ;
- health/readiness du transport ;
- fixtures de test répétitives.

### 3.3 Risques du code copié

| Risque | Conséquence possible |
|---|---|
| ACK exécuté trop tôt | Perte d’un effet métier après crash |
| Scope absent ou partagé | Utilisation concurrente d’un `DbContext` non thread-safe |
| Inbox incomplète | Double effet lors d’une redelivery QoS 1 |
| Publication/commit mal ordonnés | Perte ou duplication d’événement sortant |
| Retry non classifié | Boucle infinie sur une erreur définitive |
| Corrélation non propagée | Traces distribuées impossibles à reconstruire |
| Request/reply par requête | Race avant SUBACK et multiplication des subscriptions |
| Configuration locale divergente | Comportements différents entre services |

## 4. Décision structurante : composition plutôt qu’héritage

### 4.1 Approche déconseillée

Une hiérarchie de ce type paraît simple au départ :

```csharp
public sealed class OrderWorker : WorkerServiceBase
{
    protected override Task ProcessAsync(...);
}
```

Elle conduit progressivement à une classe de base connaissant DI, MQTT, EF, HTTP, logging, retry,
health checks et tous les cas particuliers. Les workload types hériteraient ou redéfiniraient une
partie de ce comportement. Les tests et évolutions deviendraient dépendants d’une hiérarchie fragile.

### 4.2 Approche recommandée

Créer des capacités indépendantes et des profils qui choisissent leurs valeurs par défaut :

```csharp
workload
    .UseWorkerService()
    .UseInbox()
    .UseOutbox()
    .UseDeadLetter()
    .UseOpenTelemetry();
```

Les profils ne sont pas des superclasses. Ils sont des ensembles cohérents de registrations,
validations et politiques.

### 4.3 Bénéfices

- mêmes capacités réutilisées entre Worker, WebService et Gateway ;
- remplacement individuel d’un provider ;
- tests unitaires des politiques indépendamment du Host ;
- defaults centralisés sans bloquer les cas avancés ;
- absence d’état caché dans une classe héritée ;
- migration progressive des services existants.

## 5. Architecture cible

```text
Service applicatif
  ├─ Domain
  ├─ Application
  ├─ Event Entities et topics
  └─ mappings spécifiques
            │
            ▼
Net.Mqtt.Workloads
  ├─ modèle de cas d’utilisation
  ├─ pipelines consommateurs/responders
  ├─ policies de fiabilité
  ├─ profils de workloads
  └─ observabilité et diagnostics
            │
            ▼
Net.Mqtt.Infrastructure
  ├─ MQTT bus
  ├─ CloudEvents
  ├─ schemas
  ├─ TopicSet / RequestSet
  └─ lifecycle connexion/subscription
            │
            ▼
MQTTnet / Generic Host / ASP.NET Core / EF Core
```

## 6. Découpage des packages NuGet

### 6.1 `Net.Mqtt.Workloads.Abstractions`

Package minimal, stable et sans dépendance à EF Core.

Contenu proposé :

- `IUseCase<TRequest,TResponse>` ;
- contrats de mapping optionnels ;
- décisions de traitement ;
- classification des erreurs ;
- contexte de message indépendant du transport concret ;
- abstractions inbox/outbox ;
- options de retry, lease et rétention.

### 6.2 `Net.Mqtt.Workloads.Hosting`

Dépend de `Net.Mqtt.Infrastructure` et du Generic Host.

Contenu proposé :

- builder fluent du workload ;
- pipeline `Consume` ;
- gestion automatique des scopes ;
- Hosted Services génériques ;
- responder request/reply ;
- propagation de métadonnées ;
- graceful shutdown et backpressure ;
- validation de la configuration du pipeline ;
- diagnostics au démarrage.

### 6.3 `Net.Mqtt.Workloads.EntityFrameworkCore`

Package optionnel.

Contenu proposé :

- entités et mappings inbox/outbox ;
- index unique `(source,id)` ;
- lease et concurrence optimiste ;
- unité de travail coordonnée ;
- dispatcher outbox ;
- cleanup/rétention ;
- helpers de migration ;
- health check de persistance.

Le package ne doit pas imposer un provider SQL particulier.

### 6.4 `Net.Mqtt.Workloads.AspNetCore`

Package optionnel pour WebService et WebApplication.

Contenu proposé :

- HTTP vers MQTT request/reply ;
- readiness MQTT ;
- mapping timeout vers HTTP 504 ;
- indisponibilité vers HTTP 503 ;
- Problem Details ;
- propagation de l’annulation HTTP ;
- authentification/autorisation via les mécanismes ASP.NET Core.

### 6.5 `Net.Mqtt.Workloads.Testing`

Contenu proposé :

- `WorkloadTestHost` ;
- transport InMemory préconfiguré ;
- horloge contrôlable ;
- fakes inbox/outbox ;
- publication et attente sans race ;
- assertions CloudEvents, ACK et corrélation ;
- scénarios redelivery/crash ;
- fixtures pour broker réel.

### 6.6 `Net.Mqtt.Workloads.Templates`

Package de type `Template` installable avec `dotnet new`. Les templates doivent dépendre des packages
précédents et ne plus copier leur implémentation interne.

## 7. Modèle applicatif minimal

### 7.1 Cas d’utilisation

```csharp
public interface IUseCase<in TRequest, TResponse>
{
    Task<TResponse> ExecuteAsync(
        TRequest request,
        CancellationToken cancellationToken);
}
```

L’interface doit rester indépendante de MQTT. Un handler peut être appelé depuis un Worker, une API
HTTP, un job ou un test.

### 7.2 Mapping

Deux formes doivent être supportées :

1. lambda pour un mapping simple ;
2. interface injectable pour un mapping complexe ou testable isolément.

```csharp
public interface IMessageMapper<in TMessage, out TRequest>
{
    TRequest Map(TMessage message);
}
```

### 7.3 Classification des erreurs

```csharp
public interface IMessageFailureClassifier
{
    FailureDecision Classify(Exception exception);
}
```

Décisions possibles : retry immédiat, retry différé, dead-letter, quarantine, arrêt du service ou
propagation. Les defaults doivent être conservateurs et observables.

## 8. API de consommation proposée

```csharp
builder.Services.AddMqttWorkload<OrderProcessingMqttContext>(workload =>
{
    workload.UseWorkerService();

    workload.UseEntityFrameworkReliability<OrdersDbContext>(reliability =>
    {
        reliability.UseInbox();
        reliability.UseOutbox();
        reliability.MaximumAttempts = 5;
        reliability.ProcessingLease = TimeSpan.FromMinutes(5);
    });

    workload.Consume<OrderSubmitted>()
        .From(context => context.SubmittedOrders)
        .MapTo(message => new ProcessOrderCommand(
            message.OrderId,
            message.CustomerId,
            message.Total,
            message.SubmittedAt))
        .HandleWith<IProcessOrder>()
        .Publish<OrderProcessed>(
            context => context.ProcessedOrders,
            result => new OrderProcessed
            {
                OrderId = result.OrderId,
                Status = result.Status.ToString(),
                Action = result.Action
            });
});
```

### 8.1 Validations au démarrage

Le builder doit refuser :

- un handler non enregistré ;
- un topic absent du contexte ;
- une Event Entity non gouvernée ;
- une sortie sans schema ;
- une inbox EF sans `DbContext` ;
- un outbox sans dispatcher ;
- un timeout ou une capacité non positive ;
- des lifetimes incompatibles ;
- deux pipelines utilisant une identité conflictuelle.

### 8.2 Escape hatches

Une abstraction industrielle doit permettre les cas avancés :

- middleware avant/après handler ;
- stratégie de partitionnement ;
- concurrence configurable ;
- suppression de publication ;
- ACK manuel explicitement opt-in ;
- provider custom inbox/outbox ;
- enrichissement CloudEvents ;
- codec ou schema resolver spécifique.

Ces options doivent être explicites et ne pas affaiblir silencieusement les garanties par défaut.

## 9. Pipeline WorkerService cible

```text
Subscription confirmée
  → lecture bornée
  → contexte de log/trace
  → scope DI
  → réservation inbox
  → exécution du cas d’utilisation
  → écriture de l’effet métier
  → écriture outbox
  → inbox COMPLETED
  → commit transactionnel
  → ACK MQTT
  → dispatcher outbox
  → publication MQTT
  → marquage outbox publié
```

### 9.1 Redelivery

- `COMPLETED` : ACK sans réexécuter le métier ;
- `PROCESSING` avec lease actif : ne pas concurrencer ;
- lease expiré : reprise contrôlée ;
- `RETRY_SCHEDULED` arrivé à échéance : nouvelle tentative ;
- `DEAD_LETTERED` : ne pas réexécuter automatiquement.

### 9.2 Concurrence

La concurrence doit être bornée et configurable. Chaque message possède son propre scope et son
propre `DbContext`. L’ordre doit être conservé lorsque le contrat le nécessite. Le pipeline doit
attendre les tâches en vol pendant l’arrêt.

## 10. Inbox et outbox

### 10.1 Inbox

Données minimales : source, id, type, réception, état, tentatives, lease, erreur normalisée et date de
finalisation. La clé unique est `(source,id)`.

### 10.2 Outbox

Données minimales : identifiant, type, destination logique, payload gouverné, métadonnées CloudEvents,
date de création, tentatives, prochaine tentative, état et date de publication.

### 10.3 Garantie réaliste

L’objectif n’est pas « exactly once » sur l’ensemble du système. L’objectif est :

- effet métier local idempotent ;
- écriture métier/inbox/outbox atomique ;
- publication outbox au moins une fois ;
- consommateurs aval idempotents.

### 10.4 Transactions

Le pipeline EF doit utiliser une stratégie compatible avec le provider. Il doit documenter le
comportement lorsque le provider ne supporte pas les transactions. EF InMemory ne doit pas être
considéré comme preuve de fiabilité transactionnelle.

## 11. Profils de workloads

### 11.1 `WorkerService`

Defaults : consommateurs permanents, session persistante, backpressure, inbox/outbox, graceful
shutdown et readiness du transport.

### 11.2 `WebService`

Defaults : ASP.NET Core, endpoints système, HTTP vers cas d’utilisation ou request/reply MQTT,
timeouts, Problem Details et readiness.

### 11.3 `DeviceService`

Defaults : connectivité intermittente, session persistante, Last Will, disponibilité, commandes et
buffer local optionnel. Les capacités hardware doivent rester dans des adaptateurs dédiés.

### 11.4 `GatewayService`

Defaults : haute concurrence bornée, routing, traduction, isolation par destination, rate limiting et
politiques de sécurité. Les règles métier de l’entreprise ne doivent pas migrer dans le gateway.

### 11.5 `WebApplication`

Defaults : BFF/UI, authentification, orchestration d’API et request/reply. Aucun consommateur permanent
par défaut.

### 11.6 `BackgroundJob`

Defaults : exécution finie, checkpoint, résultat explicite, arrêt du Host après completion et absence
de listener permanent.

### 11.7 `Function`

Defaults : invocation courte et stateless. Une connexion MQTT persistante ne doit pas être supposée ;
préférer un publisher adapté à la plateforme ou un gateway externe.

## 12. HTTP et request/reply

API cible possible :

```csharp
app.MapMqttRequest<OrderRequest, OrderResponse>(
    "/orders/{orderId}",
    context => context.OrderRequest,
    (string orderId) => new OrderRequest { OrderId = orderId });
```

La bibliothèque doit gérer :

- readiness de la subscription avant publication ;
- correlationid ;
- timeout et annulation HTTP ;
- réponse 503 si MQTT indisponible ;
- réponse 504 en cas de timeout ;
- mapping configurable des erreurs métier ;
- métriques de latence et nombre d’appels en vol.

## 13. Observabilité

### 13.1 Logs structurés

Champs minimaux : source, id, type, topic, correlationid, causationid, trace, tentative, pipeline,
handler, durée et résultat.

### 13.2 Tracing

Créer ou poursuivre une activité par livraison. Propager `traceparent` et `tracestate` dans l’outbox
et les publications dérivées.

### 13.3 Métriques

- messages reçus/complétés/échoués ;
- redeliveries et duplications évitées ;
- durée du handler ;
- taille des buffers ;
- âge du plus ancien outbox ;
- tentatives et dead-letter ;
- état de connexion et reconnexions ;
- requêtes request/reply en vol et timeouts.

### 13.4 Health checks

`/live` vérifie que le processus répond. `/ready` vérifie au minimum connexion MQTT, subscriptions,
persistance et dispatcher outbox. Une dépendance métier externe ne doit être incluse que si son
indisponibilité interdit réellement tout travail utile.

## 14. Sécurité

- ne jamais journaliser les payloads sensibles ;
- conserver la validation des champs interdits ;
- appliquer TLS/mTLS et ACL au niveau transport ;
- injecter les secrets par le runtime ;
- limiter la taille des messages ;
- valider schemas et versions avant handler ;
- protéger les endpoints de diagnostic ;
- limiter les détails d’exception persistés ;
- prévoir quarantine pour les messages non fiables.

## 15. Testing

### 15.1 Tests du package

Le package doit avoir une matrice plus large que les services :

- ACK uniquement après succès ;
- exception avant/après handler ;
- conflit unique inbox ;
- lease actif et expiré ;
- retry et dead-letter ;
- crash avant/après commit ;
- reprise outbox ;
- duplication de publication ;
- annulation et arrêt ;
- concurrence et backpressure ;
- request/reply sans race ;
- propagation de tracing ;
- validation des builders.

### 15.2 Tests des services

Les équipes conservent :

- tests Domain ;
- tests Application avec fakes ;
- tests de mapping ;
- tests de contrats et topics ;
- tests d’intégration des adaptateurs propres ;
- un scénario end-to-end représentatif.

### 15.3 Environnements réels

Ajouter des tests avec broker et base relationnelle réels. Le transport et EF InMemory ne couvrent
pas sessions persistantes, contraintes SQL, transactions, ACL ou coupures réseau.

## 16. Versioning et compatibilité

### 16.1 Packages

Utiliser Semantic Versioning. Une rupture d’API publique ou de comportement garanti nécessite une
version majeure. Les profils doivent publier leurs defaults et leurs changements.

### 16.2 Event contracts

Le package ne doit pas versionner à la place des services. Il doit valider les attributs, schemas et
politiques de compatibilité définis par l’équipe propriétaire.

### 16.3 Persistance

Les évolutions inbox/outbox nécessitent des migrations compatibles. Le package EF doit fournir les
mappings et une stratégie de migration documentée, sans appliquer automatiquement une migration en
production.

## 17. Expérience développeur cible

Après abstraction, un développeur devrait modifier principalement :

```text
Domain/
  Order.cs
  OrderPolicy.cs

Application/
  ProcessOrder.cs
  IOrderRepository.cs

Messaging/
  OrderEvents.cs
  OrderProcessingMqttContext.cs

Infrastructure/
  OrderRepository.cs
  OrdersDbContext.cs

Worker/
  Program.cs avec déclaration du pipeline
```

Le template ne doit plus contenir les implémentations génériques d’inbox, outbox, consumer ou
responder.

## 18. Migration du template actuel

### Étape 1 — Caractériser le comportement

Créer des tests de caractérisation pour le pipeline actuel : succès, duplicate, retry, exception,
request/reply, corrélation et arrêt.

### Étape 2 — Extraire les abstractions

Créer `Net.Mqtt.Workloads.Abstractions` avec une surface minimale. Ne pas déplacer EF ou Hosting dans
ce premier package.

### Étape 3 — Extraire le pipeline Hosting

Transformer `OrderConsumerWorker` et `CommandResponderWorker` en composants génériques configurés par
des descriptors immuables validés au démarrage.

### Étape 4 — Implémenter EF inbox/outbox

Déplacer l’inbox vers le package EF, corriger les états terminaux et ajouter l’outbox avant de déclarer
la fiabilité complète.

### Étape 5 — Migrer OrderProcessing

Remplacer les Hosted Services et l’inbox copiés par l’API du package. Comparer les tests de
caractérisation avant/après.

### Étape 6 — Réduire le template

Supprimer le code transversal, mettre à jour documentation, Docker et tests, puis republier le
template NuGet.

### Étape 7 — Ajouter AspNetCore

Extraire le pattern HTTP vers MQTT depuis les demos existantes après stabilisation du Worker.

### Étape 8 — Généraliser les profils

Créer les autres profils uniquement à partir de cas réels et de capacités déjà éprouvées.

## 19. Roadmap proposée

| Phase | Livrable | Critère de sortie |
|---|---|---|
| 0 | ADR et tests de caractérisation | Comportement actuel mesurable |
| 1 | `Workloads.Abstractions` | API minimale revue et stable |
| 2 | `Workloads.Hosting` | Consumer et request/reply génériques |
| 3 | `Workloads.EntityFrameworkCore` | Inbox/outbox testés avec SQL réel |
| 4 | Migration du template | Réduction significative du code technique |
| 5 | `Workloads.Testing` | Tests services simplifiés et sans race |
| 6 | `Workloads.AspNetCore` | HTTP request/reply industrialisé |
| 7 | Profils supplémentaires | Au moins un cas réel validé par profil |

## 20. Backlog technique initial

1. Écrire l’ADR composition versus héritage.
2. Inventorier les garanties de `Net.Mqtt.Infrastructure` réutilisables.
3. Définir `IUseCase` et les descriptors de pipeline.
4. Définir la validation du builder.
5. Extraire le scope et la boucle de consommation.
6. Concevoir la state machine inbox complète.
7. Concevoir l’entité et le dispatcher outbox.
8. Définir la classification d’erreurs.
9. Ajouter métriques et traces.
10. Créer les tests de crash/redelivery.
11. Migrer OrderProcessing comme service pilote.
12. Mesurer lignes supprimées et temps d’onboarding.

## 21. Risques de la nouvelle abstraction

| Risque | Mitigation |
|---|---|
| API fluente trop magique | Descriptors inspectables et diagnostics au démarrage |
| Abstraction trop générique | Commencer avec WorkerService et un service pilote |
| Couplage EF involontaire | Package EF strictement optionnel |
| Defaults modifiés entre versions | Versioning et changelog comportemental |
| Cas avancés bloqués | Middleware, providers custom et escape hatches explicites |
| Faux sentiment de exactly-once | Documentation claire inbox/outbox/at-least-once |
| Pipeline difficile à déboguer | Logs, traces et vue descriptive du pipeline |
| Explosion de packages | Frontières cohérentes et package meta optionnel |

## 22. Critères d’acceptation

La première version peut être considérée prête lorsque :

- un service ne contient plus de boucle de consumer manuelle ;
- aucun scoped n’est injecté directement dans un Hosted Service ;
- inbox, outbox, commit et ACK ont des tests de crash ;
- request/reply attend la subscription sans code applicatif ;
- tous les pipelines sont validés et décrits au démarrage ;
- la corrélation et le tracing sont automatiques ;
- le service pilote conserve ses comportements fonctionnels ;
- une base SQL et un broker réels passent les tests d’intégration ;
- le template généré se compile et s’exécute sous Docker ;
- la documentation permet à une équipe de créer une règle métier sans comprendre l’implémentation
  interne du pipeline ;
- les mécanismes avancés restent accessibles sans fork de la bibliothèque.

## 23. Indicateurs de succès

- nombre de lignes techniques supprimées du template ;
- temps entre génération et premier cas d’utilisation opérationnel ;
- nombre de registrations DI nécessaires par service ;
- nombre d’incidents ACK/redelivery/duplicate ;
- couverture des scénarios de fiabilité ;
- délai de mise à jour de la plateforme sur tous les services ;
- nombre de variantes locales du pipeline ;
- satisfaction et autonomie des équipes applicatives.

## 24. Recommandation finale

Créer la nouvelle couche d’abstraction est justifié. Le template actuel doit devenir le service pilote
et la spécification comportementale de cette extraction. Il ne faut toutefois pas chercher à masquer
le métier ni à créer une classe mère par workload.

La trajectoire recommandée est :

1. stabiliser les primitives de cas d’utilisation ;
2. extraire le pipeline `WorkerService` par composition ;
3. intégrer inbox et outbox EF Core ;
4. fournir les outils de test ;
5. réduire le template ;
6. ajouter ensuite AspNetCore et les autres profils à partir de cas concrets.

Cette stratégie déplace la complexité vers un produit de plateforme versionné, testé et observable.
Les équipes applicatives conservent les décisions métier et les contrats, tout en bénéficiant de
garanties homogènes sur l’ensemble de l’écosystème distribué.
