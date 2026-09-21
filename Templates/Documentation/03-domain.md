# Projet Domain

[Retour à l’index](index.md)

## Responsabilité

Domain contient le langage et les invariants métier. Il ne connaît ni MQTT, ni CloudEvents, ni EF
Core, ni HTTP, ni injection de dépendances. Cette indépendance permet de tester les règles sans Host
et de réutiliser le métier depuis un autre adaptateur.

## Éléments du template

### `Order`

L’agrégat vérifie ses invariants à la construction : identifiants obligatoires et total strictement
positif. Ses setters privés empêchent un adaptateur de créer un état invalide. Le constructeur privé
existe uniquement pour le matérialiseur EF Core.

### `OrderPolicy`

`Evaluate` est une fonction pure. Elle reçoit l’agrégat et le profil client minimal, puis retourne
une `OrderDecision`. Elle ne persiste rien et ne publie aucun événement.

### `CustomerProfile` et `OrderDecision`

Ces records représentent des concepts nécessaires à la règle. `CustomerProfile` n’est pas la DTO
HTTP complète du système externe : il ne contient que les données dont Domain a besoin.

## Bonnes pratiques d’adaptation

- Utiliser des Value Objects lorsque plusieurs champs forment un invariant.
- Préférer des méthodes métier à des setters publics.
- Émettre une exception Domain explicite pour une règle violée.
- Garder les politiques déterministes ; passer l’heure ou l’aléatoire comme valeur/abstraction.
- Ne jamais ajouter d’attribut `[MqttTopic]`, `[EventType]` ou EF dans ce projet.

## Tests attendus

Chaque branche d’une règle doit avoir un test pur : cas nominal, limites, valeurs invalides et
transitions interdites. Aucun mock framework n’est nécessaire pour `OrderPolicy`.
