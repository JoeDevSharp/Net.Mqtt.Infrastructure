# Vue d’ensemble et dépendances

[Retour à l’index](index.md)

## Objectif

Le template fournit un Worker Service applicatif qui reçoit une commande par MQTT, exécute un cas
d’utilisation, persiste son effet, publie un résultat puis acquitte la livraison. Il expose aussi une
consultation MQTT request/reply. L’exemple sert à montrer les frontières ; il n’impose pas le domaine
Sales aux futurs services.

## Organisation logique

```text
src
├─ Core
│  ├─ Domain
│  └─ Application
├─ Adapters
│  ├─ Messaging
│  └─ Infrastructure
└─ Host
   └─ Worker

tests
├─ Unit
│  ├─ Domain.Tests
│  └─ Application.Tests
└─ Integration
   └─ Worker.IntegrationTests
```

Les dossiers de solution sont logiques. Les projets restent physiquement sous `src/` et `tests/`.

## Direction des références

```text
Worker ───────► Application ───────► Domain
   │
   ├─────────► Messaging ──────────► Net.Mqtt.Infrastructure
   │
   └─────────► Infrastructure ─────► Application + Domain
```

Les interdictions sont aussi importantes que les références :

- Domain ne référence aucune autre couche ;
- Application ne référence ni Infrastructure, ni Messaging ;
- Messaging ne référence pas les modèles Domain ;
- Infrastructure ne connaît pas les Hosted Services ;
- seul Worker compose les abstractions avec leurs implémentations.

## Modèles distincts

| Modèle | Projet | Exemple | Finalité |
|---|---|---|---|
| Transport | Messaging | `OrderSubmitted` | Contrat CloudEvents/JSON transmis sur MQTT |
| Application | Application | `ProcessOrderCommand` | Entrée stable du cas d’utilisation |
| Domaine | Domain | `Order` | Invariants et comportement métier |
| Persistance | Infrastructure | `InboxEntry` et mapping EF | Stockage technique |

Ne pas réutiliser une Event Entity comme entité EF ou objet Domain. La conversion explicite dans le
Worker empêche une évolution du transport de contaminer les règles métier.

## Où ajouter une fonctionnalité

| Besoin | Emplacement |
|---|---|
| Nouvelle règle métier | Domain |
| Nouveau cas d’utilisation ou port | Application |
| Nouveau contrat/topic MQTT | Messaging |
| Nouveau provider SQL, API HTTP ou stockage objet | Infrastructure |
| Nouvelle boucle de consommation ou responder | Worker |
| Nouveau test pur | Projet de tests correspondant à la couche |
