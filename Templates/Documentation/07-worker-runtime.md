# Projet Worker et cycle d’un message

[Retour à l’index](index.md)

## Responsabilité du Host

Worker est l’exécutable et la racine de composition. Ses Hosted Services sont des adaptateurs
entrants : ils traduisent MQTT vers Application, mais n’implémentent pas les règles métier.

## Cycle de `OrderConsumerWorker`

```text
Livraison MQTT
  └─► scope DI par message
       └─► TryBegin inbox
            ├─ déjà COMPLETED ─► ACK sans nouvel effet
            ├─ déjà PROCESSING ─► pas d’ACK
            └─ Started
                 └─► rendre PROCESSING durable
                      └─► IProcessOrder
                           └─► publier OrderProcessed
                                └─► inbox COMPLETED + SaveChanges
                                     └─► ACK MQTT
```

L’ordre est volontaire : un effet métier échoué n’est jamais acquitté. La publication précède l’ACK
et la finalisation de l’unité de travail précède également l’ACK.

## Conversion des modèles

Le Worker convertit explicitement `OrderSubmitted` en `ProcessOrderCommand`, puis
`ProcessOrderResult` en `OrderProcessed`. Cette traduction est la frontière entre transport et
application. Y placer uniquement le mapping, pas les décisions métier.

## Corrélation et observabilité

Le scope de log inclut source, id, type, correlationid et topic. Lors de la publication dérivée, le
Worker propage :

- `CorrelationId` pour suivre l’opération globale ;
- `CausationId` avec l’id du message reçu ;
- `TraceParent` et `TraceState` pour le tracing distribué.

Les payloads complets ne doivent pas être journalisés s’ils peuvent contenir des données sensibles.

## Gestion des erreurs

- Une annulation demandée par le Host est relancée pour permettre un arrêt propre.
- Une autre exception est journalisée et enregistrée dans l’inbox.
- Le message n’est pas acquitté afin de permettre la redelivery QoS 1.
- Le traitement reste séquentiel par défaut pour préserver un comportement prévisible.

Pour ajouter du parallélisme, borner la concurrence et conserver un scope/DbContext distinct par
message. Ne jamais lancer un `Task.Run` non suivi.

## `CommandResponderWorker`

Le responder utilise `HandleAsync`, crée un scope par requête, appelle `IReadOrder`, publie la réponse
corrélée puis laisse la bibliothèque acquitter la requête réussie. Il réutilise une souscription
partagée pendant toute la vie du service.
