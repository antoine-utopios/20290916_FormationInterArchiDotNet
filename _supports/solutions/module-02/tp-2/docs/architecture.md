# Trame 2 — squelette de solution (TP 2)

Solution .NET 10 produite au TP 2 du module « Intégration par messages et plateforme .NET ».

| Projet | Rôle | Peut référencer |
|---|---|---|
| `Textinord.Trame.Domain` | entités, règles métier, ports du domaine | rien |
| `Textinord.Trame.Application` | cas d'usage, messages, bus in-process, pipeline | Domain |
| `Textinord.Trame.Infrastructure` | dépôts, passerelles, hébergement du bus | Application, Domain |
| `Textinord.Trame.Api` | Minimal API, OpenAPI, supervision | Application, Infrastructure |
| `Textinord.Trame.Worker` | hôte générique, simulation du flux | Application, Infrastructure |

Flux de commande : `commandes.validees` → routeur par contenu (France / export) →
splitter + traducteur (un ordre par entrepôt, au format WMS) → `entrepot.RBX` /
`entrepot.LSQ` → passerelle entrepôt. Wire tap vers `audit`, dead letter pour tout
message expiré, rejeté ou en échec.

Commandes utiles : `dotnet build`, `dotnet test`, `dotnet run --project src/Textinord.Trame.Worker`,
`dotnet run --project src/Textinord.Trame.Api` puis `http://localhost:5210/openapi/v1.json`.
