# Projet Application

[Retour à l’index](index.md)

## Responsabilité

Application orchestre les cas d’utilisation et définit les ports dont elle a besoin. Elle décide
« quoi faire », mais pas comment accéder à SQL, HTTP ou MQTT.

## Cas d’utilisation

`ProcessOrderCommand` est construit par le Worker depuis `OrderSubmitted`. `IProcessOrder` expose
le contrat d’exécution et `ProcessOrderHandler` :

1. recherche une commande existante ;
2. charge le profil client via `ICustomerCatalogService` ;
3. construit l’agrégat Domain ;
4. délègue la décision à `OrderPolicy` ;
5. ajoute l’agrégat via `IOrderRepository` ;
6. retourne un résultat indépendant du transport.

Le handler n’appelle pas `SaveChangesAsync`. La frontière transactionnelle appartient à
l’adaptateur qui coordonne inbox et cas d’utilisation.

`IReadOrder` constitue un second cas d’utilisation, utilisé par le responder MQTT. Une future API
HTTP pourrait réutiliser le même port sans dépendre de Messaging.

## Ports

| Port | Nature | Implémentation actuelle |
|---|---|---|
| `IOrderRepository` | Persistance d’agrégat | `OrderRepository` EF Core |
| `IOrderUnitOfWork` | Validation des changements | `OrderUnitOfWork` |
| `IInbox` | Idempotence des livraisons | `EfInbox` |
| `ICustomerCatalogService` | Gateway vers un système externe | `CustomerCatalogService` HTTP |

Les interfaces restent dans Application parce que le besoin appartient au cas d’utilisation.
Infrastructure dépend de ces abstractions pour les implémenter, jamais l’inverse.

## Ajouter un cas d’utilisation

1. Créer une commande/requête et un résultat applicatifs.
2. Définir une interface d’exécution avec `CancellationToken`.
3. Implémenter l’orchestration sans technologie d’adaptation.
4. Ajouter les ports manquants sous `Abstractions`.
5. Enregistrer le handler dans `AddOrderProcessingApplication`.
6. Écrire un test avec des fakes des ports.
7. Ajouter ensuite l’adaptateur entrant dans Worker ou une autre couche Host.
