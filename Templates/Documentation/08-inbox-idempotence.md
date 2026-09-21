# Inbox, idempotence et unité de travail

[Retour à l’index](index.md)

## Pourquoi une inbox

QoS 1 garantit une livraison au moins une fois. Une reconnexion, une absence d’ACK ou un crash peut
présenter plusieurs fois le même CloudEvent. L’inbox empêche de répéter les effets métier en utilisant
la clé stable `source + id`.

## Données persistées

`InboxEntry` contient source, id, type, date de réception, état, tentatives, date de dernière tentative,
dernière exception normalisée et date de finalisation.

## États du template

```text
PROCESSING ──► COMPLETED
     │
     ├──────► RETRY_SCHEDULED
     └──────► DEAD_LETTERED
```

`TryBeginAsync` crée l’entrée ou reprend un retry. Une entrée `COMPLETED` permet un ACK immédiat sans
réexécution. Une entrée `PROCESSING` récente conserve son lease ; après cinq minutes, elle peut être
reprise pour récupérer d’un crash.

`FailAsync` incrémente la stratégie d’échec : avant cinq tentatives, l’état autorise un retry ; à la
cinquième, il devient dead-lettered. Adapter seuils et délais à la criticité du service.

## Coordination transactionnelle

Le même `OrdersDbContext` scoped suit l’agrégat et l’entrée d’inbox. La transition `COMPLETED` et les
changements métier sont validés ensemble par `IOrderUnitOfWork.SaveChangesAsync`.

La réservation `PROCESSING` est rendue durable séparément pour rendre visible le lease. En cas
d’échec, un nouveau scope persiste l’exception sans valider les modifications métier non terminées.

## Limite : publication MQTT

Une transaction SQL ne peut pas rendre atomique une publication MQTT. Un crash entre publication et
commit peut republier le résultat lors du retry ; un crash dans l’ordre inverse peut perdre le résultat.
Pour une garantie de livraison forte, ajouter un outbox dans le même DbContext :

1. écrire l’événement sortant avec l’effet métier et `COMPLETED` ;
2. committer la transaction ;
3. publier l’outbox depuis un dispatcher ;
4. marquer l’entrée outbox publiée ;
5. rendre les consommateurs du résultat eux-mêmes idempotents.

## Contraintes de production

- Créer un index/PK unique sur `(Source, Id)`.
- Gérer le conflit unique entre traitements concurrents.
- Normaliser les exceptions sans données sensibles.
- Distinguer erreurs transitoires et définitives.
- Prévoir rétention et purge des entrées terminées.
- Tester crash avant/après commit et avant/après ACK avec un broker et une base réels.
