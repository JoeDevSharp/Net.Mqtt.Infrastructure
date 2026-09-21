# Projet Messaging

[Retour à l’index](index.md)

## Responsabilité

Messaging décrit la frontière MQTT : contrats de transport, schemas, topics, hiérarchie et
request/reply. Il ne contient aucune règle métier et ne référence ni Application ni Domain.

## Event Entities

Les records `OrderSubmitted`, `OrderProcessed`, `ReadOrder` et `OrderResponse` sont gouvernés par
des attributs :

- `[EventType]` définit le type CloudEvents stable ;
- `[DataSchema]` associe une URI absolue ;
- `[EventVersion]` versionne le contrat ;
- `[MaximumDataSize]` borne le payload ;
- `[SchemaCompatibility]` exprime la politique de compatibilité ;
- `[ForbiddenField]` bloque les champs sensibles.

Une Event Entity ne doit pas devenir une entité Domain ou EF. Toute modification publiée doit être
traitée comme une évolution de contrat inter-systèmes.

## Schemas

`OrderProcessingSchemas` centralise les JSON Schemas enregistrés par URI. Chaque Event Entity doit
avoir un schema enregistré avec la même URI et une version cohérente. Le démarrage ou le traitement
échoue explicitement si une entité ou son schema manque.

En production, préférer des schemas lisibles, versionnés et revus. Les chaînes inline du template
peuvent être remplacées par des fichiers embarqués ou un resolver gouverné.

## Contexte MQTT

`OrderProcessingMqttContext` déclare quatre `TopicSet<T>` : soumission, résultat, demande de lecture
et réponse. Les topics sont relatifs à :

```text
{BaseTopic}/moduls/{ModuleIdentity}/services/{ServiceIdentity}
```

Le contexte déclare aussi `OrderRequest`, un `MqttRequestSet<ReadOrder, OrderResponse>`. Cette
abstraction partage la souscription aux réponses, attend qu’elle soit prête, génère la corrélation et
applique le timeout. Ne pas créer un nouvel énumérateur de réponse pour chaque appel.

## Enregistrement

`AddOrderProcessingMessaging` :

1. lie et valide `MqttSettings` ;
2. sélectionne le transport InMemory si demandé ;
3. construit la hiérarchie de topics et la source CloudEvents ;
4. applique les valeurs de production ;
5. enregistre toutes les Event Entities et schemas.

Le contexte et le lifecycle MQTT sont administrés par la bibliothèque. Un Worker ne doit pas appeler
manuellement `ConnectAsync`.

## Évolution sûre

- Ne jamais changer la signification d’un champ existant silencieusement.
- Ajouter une nouvelle version de schema/type pour une rupture.
- Tester sérialisation, validation, topic résolu et compatibilité.
- Conserver `source`, `id`, `correlationid`, `causationid` et tracing lors des événements dérivés.
