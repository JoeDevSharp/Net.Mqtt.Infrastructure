# Configuration et environnements

[Retour à l’index](index.md)

## Sources de configuration

Le Generic Host combine `appsettings.json`, fichier d’environnement, variables d’environnement,
arguments et providers ajoutés par l’entreprise. Ne pas lire directement les variables dans un
handler ; lier des options ou injecter une abstraction.

## Section MQTT

| Clé | Finalité | Exemple local |
|---|---|---|
| `Host` | Broker MQTT | `localhost` |
| `Port` | Port TCP | `1883` |
| `ClientId` | Identité unique de connexion | `order-processing-worker-local` |
| `BaseTopic` | Racine versionnée | `mint/v1` |
| `ModuleIdentity` | Segment du module | `sales` |
| `ServiceIdentity` | Segment du service | `order-processing` |
| `CloudEventSource` | URI stable de provenance | `urn:mint:sales:order-processing` |
| `UseInMemoryTransport` | Transport sans broker | `true` uniquement local/tests |

Les options utilisent annotations, validation d’URI et `ValidateOnStart`. Un service mal configuré
doit échouer avant de commencer à consommer.

## Production

`appsettings.Production.json` désactive le transport InMemory. Les valeurs sensibles ou spécifiques
à une instance doivent venir du déploiement : secret store, configuration Kubernetes, service de
configuration ou variables protégées.

Ne pas versionner : mots de passe MQTT, certificats privés, chaînes de connexion avec secrets, tokens
HTTP ou identités d’instance réelles.

## Identités et scaling

`ClientId` doit être unique par connexion simultanée. `CloudEventSource`, en revanche, représente la
provenance logique et doit rester stable pour l’idempotence et l’observabilité. Ne pas les confondre.

Avec plusieurs replicas, définir une stratégie explicite pour les subscriptions, sessions persistantes,
shared subscriptions et distribution des messages selon les garanties du broker.

## Persistance et services externes

- `Persistence:DatabaseName` configure uniquement EF InMemory dans l’exemple.
- `CustomerCatalog:BaseUrl` doit être une URI absolue.
- Les timeouts sont configurés côté adaptateur et doivent être plus courts que le budget global.

Pour un provider relationnel, ajouter une option fortement typée ou utiliser `ConnectionStrings` sans
exposer la chaîne à Application ou Domain.

## Arrêt

Le `stoppingToken` du Host est propagé aux subscriptions, cas d’utilisation, EF, HTTP, publications et
ACK. L’orchestrateur doit accorder un délai d’arrêt suffisant pour terminer les messages en vol.
