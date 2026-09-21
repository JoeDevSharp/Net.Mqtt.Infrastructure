# Mint.Sales.WorkerService.OrderProcessing

Template de référence d’un Worker Service applicatif fondé sur `Documentation/workerservice-architecture.md`.

La documentation détaillée des projets, de l’injection de dépendances et des adaptateurs est
disponible dans le [portail documentaire du template](../Documentation/index.md).

## Génération d’un nouveau service

Depuis la racine du repository :

```powershell
dotnet new install .\Templates\Mint.Sales.WorkerService.OrderProcessing
dotnet new mqtt-worker-service --name Contoso.Sales.WorkerService.OrderProcessing --output .\services\order-processing
```

`--name` doit respecter `{Company}.{Domain}.WorkerService.{Capability}`. Le moteur remplace le nom
de solution, les projets, namespaces et références de projets.

## Adaptation

1. Remplacer le préfixe `Mint.Sales.WorkerService.OrderProcessing` par `{Company}.{Domain}.WorkerService.{Capability}`.
2. Adapter les Event Entities, schemas, topics et règles de domaine.
3. Remplacer EF Core InMemory par le provider de production et créer les migrations.
4. Configurer `Mqtt` et `CustomerCatalog` via `appsettings`, variables d’environnement ou secret store.
5. Exécuter `dotnet test {Company}.{Domain}.WorkerService.{Capability}.sln`.
6. Exécuter le Worker avec `dotnet run --project src/Mint.Sales.WorkerService.OrderProcessing.Worker`.

## Docker

Construire et démarrer le Worker avec un broker Mosquitto local :

```powershell
docker compose up --build
```

La configuration Mosquitto fournie autorise les connexions anonymes uniquement pour le développement.
Voir [la documentation Docker et Compose](../Documentation/12-containers.md) avant toute adaptation
à un environnement de production.

La configuration `Mqtt:UseInMemoryTransport=true` permet un démarrage local sans broker. En production,
elle doit être `false`; les secrets et certificats ne doivent jamais être versionnés.

Les sorties de compilation sont centralisées dans `.artifacts/bin` et `.artifacts/obj` avec des noms
courts. Cette convention évite la limite Windows `MAX_PATH` sans raccourcir les noms de projets,
namespaces ou solutions.

## Points d’extension

- `Application` définit les cas d’utilisation et les ports.
- `Domain` contient les règles métier pures.
- `Messaging` gouverne Event Entities, JSON Schemas et topics MQTT.
- `Infrastructure` fournit EF Core, inbox transactionnelle, repository et client HTTP.
- `Worker` est uniquement la racine de composition et l’adaptateur MQTT.

La solution organise ces projets dans `src/Core`, `src/Adapters`, `src/Host`, `tests/Unit` et
`tests/Integration`. Ce sont des dossiers logiques Visual Studio ; les chemins physiques restent
`src/` et `tests/`.

L’inbox utilise `(CloudEvent source, id)` comme clé unique. La décision métier, l’état `COMPLETED`
et la publication du résultat précèdent l’ACK MQTT. Pour garantir l’atomicité entre la base et une
publication MQTT, ajouter un outbox persistant avant la mise en production.
