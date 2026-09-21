# Stratégie de tests

[Retour à l’index](index.md)

## Pyramide fournie

| Projet | Portée | Dépendances exclues |
|---|---|---|
| `Domain.Tests` | Invariants et politiques pures | DI, MQTT, EF, HTTP |
| `Application.Tests` | Orchestration des cas d’utilisation | Implémentations Infrastructure |
| `Worker.IntegrationTests` | Registre, schemas, codec, topics et transport | Broker réel dans le test rapide |

## Tests Domain

`OrderPolicyTests` construit les objets directement et vérifie la décision. Ajouter un test par branche,
limite et invariant. Ces tests doivent rester rapides et déterministes.

## Tests Application

`ProcessOrderTests` utilise des fakes de `IOrderRepository` et `ICustomerCatalogService`, puis résout
le handler par les enregistrements DI réels. Il vérifie à la fois le résultat et l’effet demandé au
repository.

Préférer un fake lisible à un mock excessif. Vérifier aussi annulation, erreurs du gateway, doublon de
commande et absence de persistance lorsque la règle échoue.

## Tests Messaging InMemory

`MessagingTests` démarre un Host avec `UseInMemoryTransport`, commence la lecture avant publication,
publie une Event Entity valide, vérifie la livraison puis l’acquitte. Ce niveau couvre :

- validation des attributs Event Entity ;
- résolution du JSON Schema ;
- sérialisation CloudEvents ;
- modèle de topic ;
- subscription et ACK.

## Tests supplémentaires obligatoires en production

Le transport InMemory ne remplace pas :

- tests avec le broker et sa version réels ;
- session persistante, reconnexion et redelivery QoS 1 ;
- ACL, TLS/mTLS et Last Will ;
- provider SQL réel, migrations et contraintes uniques ;
- concurrence sur l’inbox ;
- crash aux frontières commit/publication/ACK ;
- contrat réel du service HTTP externe ;
- charge, backpressure et arrêt avec messages en vol.

## Commandes

```powershell
dotnet restore Mint.Sales.WorkerService.OrderProcessing.sln
dotnet build Mint.Sales.WorkerService.OrderProcessing.sln --no-restore
dotnet test Mint.Sales.WorkerService.OrderProcessing.sln --no-build --no-restore
```

Les artefacts sont centralisés sous `.artifacts` afin d’éviter `MAX_PATH` sous Windows.
