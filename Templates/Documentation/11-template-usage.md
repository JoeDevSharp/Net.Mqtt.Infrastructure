# Industrialisation avec `dotnet new`

[Retour à l’index](index.md)

## Installation

Depuis la racine du repository :

```powershell
dotnet new install .\Templates\Mint.Sales.WorkerService.OrderProcessing
```

Le manifeste expose le nom court `mqtt-worker-service`.

## Génération

```powershell
dotnet new mqtt-worker-service `
  --name Contoso.Billing.WorkerService.InvoiceProcessing `
  --output .\services\invoice-processing
```

Le nom doit respecter `{Company}.{Domain}.WorkerService.{Capability}`. Le moteur remplace le nom de
solution, projets, namespaces et références internes. Les dossiers `.vs`, `bin`, `obj`, `.artifacts`
et `.template.config` ne sont pas copiés dans le service généré.

Dans le repository de la bibliothèque, Messaging utilise une `ProjectReference` locale. Hors de ce
repository, le `.csproj` sélectionne automatiquement le package `Net.Mqtt.Infrastructure`.

## Checklist après génération

1. Remplacer `Order`, `Customer` et `Sales` par le langage du bounded context.
2. Redéfinir Event Entities, types CloudEvents et URIs de schemas.
3. Déclarer les topics relatifs requis.
4. Implémenter les règles Domain et cas d’utilisation Application.
5. Remplacer les fakes fonctionnels de l’exemple par les adaptateurs nécessaires.
6. Choisir un provider EF de production et créer les migrations.
7. Adapter retry, lease inbox, seuil dead-letter et politique de rétention.
8. Ajouter un outbox lorsque la publication fiable est exigée.
9. Configurer broker, TLS, ACL, secrets, observabilité et health/readiness.
10. Compléter les tests broker/base/services externes réels.

## Ce qui doit rester stable

- direction des références ;
- séparation transport/application/domaine/persistance ;
- scope par message ;
- propagation des tokens ;
- idempotence par `source + id` ;
- publication et validation métier avant ACK ;
- typed clients pour les ressources externes ;
- configuration validée au démarrage.

## Mise à jour du template

Une évolution transversale doit être appliquée au template, à cette documentation et à un service
pilote. Compiler la solution et exécuter tous les tests avant de réinstaller le template. Versionner
les changements incompatibles et fournir une note de migration aux équipes consommatrices.
