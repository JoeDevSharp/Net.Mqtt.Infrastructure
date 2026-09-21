# Conteneurisation avec Docker et Compose

[Retour à l’index](index.md)

## Fichiers fournis

| Fichier | Responsabilité |
|---|---|
| `Dockerfile` | Construire une image runtime minimale du Worker |
| `.dockerignore` | Exclure artefacts, secrets potentiels et fichiers IDE du contexte |
| `docker-compose.yml` | Démarrer le Worker avec un broker Mosquitto local |
| `docker/mosquitto.conf` | Configurer un broker de développement persistant et sans authentification |

Ces fichiers se trouvent à la racine du service généré. Le remplacement `sourceName` du moteur
`dotnet new` adapte automatiquement le chemin du `.csproj` et le nom de la DLL dans le Dockerfile.

## Dockerfile multi-stage

Le stage `build` utilise le SDK .NET 10 pour restaurer la solution et publier uniquement le projet
Worker en Release. Le stage final utilise `mcr.microsoft.com/dotnet/aspnet:10.0`, nécessaire parce
que les projets du template référencent le shared framework ASP.NET Core.

L’image finale :

- ne contient ni SDK, ni sources, ni résultats de tests ;
- exécute le processus avec un utilisateur Linux non-root ;
- fixe `DOTNET_ENVIRONMENT=Production` ;
- désactive les diagnostics .NET dans le conteneur par défaut ;
- lance la DLL avec un ENTRYPOINT en forme exec afin de recevoir correctement les signaux d’arrêt.

Construction depuis la racine du service :

```powershell
docker build --tag order-processing-worker:local .
```

Exécution contre un broker déjà disponible :

```powershell
docker run --rm `
  --name order-processing-worker `
  --env Mqtt__Host=host.docker.internal `
  --env Mqtt__UseInMemoryTransport=false `
  order-processing-worker:local
```

## Docker Compose local

Le compose démarre :

1. `mqtt`, basé sur Mosquitto 2 avec stockage persistant ;
2. `worker`, construit depuis le Dockerfile ;
3. le Worker uniquement après le healthcheck du broker.

```powershell
docker compose up --build
```

Arrêt sans supprimer les messages persistés du broker :

```powershell
docker compose down
```

Arrêt avec suppression du volume MQTT local :

```powershell
docker compose down --volumes
```

La dernière commande détruit les données de développement stockées dans `mqtt-data`.

## Variables d’environnement .NET

Le séparateur `__` représente `:` dans la configuration .NET. Par exemple,
`Mqtt__CloudEventSource` remplace `Mqtt:CloudEventSource`. Le compose fournit toutes les valeurs MQTT
nécessaires et force `UseInMemoryTransport=false`.

`CustomerCatalog__BaseUrl` pointe par défaut vers `host.docker.internal:7040`. Modifier cette valeur
si le catalogue est un autre service Compose :

```yaml
environment:
  CustomerCatalog__BaseUrl: http://customer-catalog:8080/
```

Dans ce cas, déclarer `customer-catalog` dans le même réseau Compose. Le Worker démarre sans appeler
le catalogue ; l’endpoint doit être disponible avant de traiter une commande.

## Persistance

Le volume `mqtt-data` conserve la session et les données Mosquitto entre redémarrages. En revanche,
le template utilise EF Core InMemory : commandes et inbox disparaissent au redémarrage du Worker.

Pour tester réellement la redelivery et l’idempotence après crash, ajouter une base relationnelle au
compose, sélectionner son provider EF Core et stocker ses données dans un volume dédié.

## Sécurité

`mosquitto.conf` active `allow_anonymous true` uniquement pour le développement local. Ne pas déployer
cette configuration en production. Une plateforme réelle doit fournir :

- authentification du client et ACL minimales par topic ;
- TLS ou mTLS avec validation de certificat ;
- secrets injectés par le runtime, jamais copiés dans l’image ;
- image Mosquitto et images .NET épinglées selon la politique de l’organisation ;
- filesystem et capabilities restreints ;
- analyse de vulnérabilités et SBOM dans la CI.

## Publication dans un registry

Exemple générique :

```powershell
docker build --tag registry.example.com/sales/order-processing:1.0.0 .
docker push registry.example.com/sales/order-processing:1.0.0
```

Utiliser une version immuable ou un digest pour les déploiements. Ne pas dépendre exclusivement du
tag `latest`.

## Validation CI recommandée

1. `dotnet restore`, `build` et `test` ;
2. `docker build` ;
3. analyse de l’image ;
4. démarrage Compose avec broker et base réels ;
5. publication d’un CloudEvent de test ;
6. vérification résultat, inbox, ACK et arrêt gracieux ;
7. suppression contrôlée de l’environnement éphémère.
