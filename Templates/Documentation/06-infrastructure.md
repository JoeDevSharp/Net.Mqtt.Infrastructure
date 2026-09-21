# Projet Infrastructure et adaptateurs

[Retour à l’index](index.md)

## Responsabilité

Infrastructure implémente les ports déclarés par Application. Elle concentre les détails EF Core,
HTTP, retry technique, sérialisation externe et configuration des providers. Aucune de ses classes
ne doit être appelée directement par Domain.

## Adaptateur EF Core

`OrdersDbContext` porte deux ensembles dans la même unité de travail :

- `Orders`, pour les effets métier ;
- `Inbox`, pour l’identité et l’état de chaque livraison.

Le mapping configure les clés, longueurs, précision décimale et conversions d’enums. La clé composite
`Source + Id` constitue la barrière d’idempotence.

Le template utilise `Microsoft.EntityFrameworkCore.InMemory` afin de démarrer sans serveur. Pour la
production :

1. ajouter le provider approuvé (`SqlServer`, PostgreSQL, etc.) ;
2. remplacer `UseInMemoryDatabase` dans `DependencyInjection` ;
3. créer et versionner les migrations ;
4. appliquer les migrations par un processus contrôlé ;
5. vérifier index unique, transactions et stratégie de résilience du provider réel.

EF InMemory ne valide pas toutes les contraintes et transactions d’une base relationnelle. Il ne
constitue pas une preuve d’intégration SQL.

## Repository et unité de travail

`OrderRepository` traduit `IOrderRepository` vers EF Core. `AddAsync` ne valide pas immédiatement :
le Worker coordonne la validation avec l’inbox via `IOrderUnitOfWork`. Un repository ne doit ni
publier sur MQTT ni appeler une API externe.

## Adaptateur HTTP

`CustomerCatalogService` est un typed `HttpClient` :

- l’adresse de base vient de la configuration ;
- le timeout est borné ;
- le `CancellationToken` est propagé ;
- une absence devient `CustomerNotFoundException`.

Dans un service réel, compléter la classification des erreurs : 4xx non rejouable, 429/5xx et timeout
potentiellement transitoires. Placer retry/circuit breaker ici, sans boucle infinie dans Application.

## Ajouter un adaptateur

1. Identifier le port Application à implémenter.
2. Créer l’implémentation dans un dossier explicite (`Persistence`, `ExternalServices`, `Storage`).
3. Convertir les DTO externes vers les modèles attendus par Application/Domain.
4. Enregistrer l’implémentation avec le lifetime approprié.
5. Ajouter timeout, annulation, observabilité et classification d’erreurs.
6. Tester avec le système réel ou un substitut au niveau intégration.
