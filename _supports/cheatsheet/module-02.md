# Cheatsheet — Module 2 : intégration par messages et plateforme .NET

## Les patterns d'intégration (Hohpe et Woolf), une phrase chacun

| Famille | Pattern | En une phrase |
|---|---|---|
| Styles | Transfert de fichiers | on dépose un fichier, l'autre le lit plus tard (EDI Textinord). |
| | Base partagée | deux applications lisent les mêmes tables : schéma figé, règles dupliquées. |
| | RPC | l'appelant attend la réponse : couplage temporel. |
| | Messaging | on dépose un message, le destinataire le traite quand il peut : découplage temps et espace. |
| Canaux | Point-à-point | un message, un seul consommateur (commandes, ordres). |
| | Publish-subscribe | un événement, une copie par abonné (faits passés). |
| | Datatype Channel | un canal par type de message. |
| | Invalid Message Channel | ce qui ne respecte pas le contrat sort du flux. |
| | Dead Letter Channel | expiré, rejeté, en échec : mis de côté avec la raison, jamais perdu. |
| | Guaranteed Delivery | le broker persiste jusqu'à l'acquittement. |
| | Channel Adapter | branche un système non messager (fichier, HTTP) sur un canal. |
| | Message Bridge | relie deux systèmes de messagerie. |
| Construction | Command Message | une intention, un destinataire, peut échouer (`ValiderCommande`). |
| | Document Message | des données, sans dire quoi en faire (`OrdrePreparationEntrepot`). |
| | Event Message | un fait passé, immuable (`CommandeValidee`). |
| | Request-Reply | deux canaux, aller et retour. |
| | Return Address | où répondre, porté par le message. |
| | Correlation Identifier | le même identifiant sur tous les messages d'un flux ; `CausationId` = le message parent. |
| | Message Sequence | position 1/n … n/n. |
| | Message Expiration | au-delà de la date, dead letter sans traitement. |
| | Format Indicator | `Type = "CommandeValidee.v1"` : faire coexister deux versions. |
| Transformation | Message Translator | convertit d'un format vers un autre, sans effet de bord. |
| | Envelope Wrapper | ajoute et retire les en-têtes de transport. |
| | Content Enricher | complète avec des données externes. |
| | Content Filter | retire ce que le destinataire ne doit pas voir. |
| | Claim Check | le gros contenu à part, une clé dans le message. |
| | Normalizer | un routeur puis un traducteur par format d'entrée. |
| | Canonical Data Model | un format pivot qui n'appartient à aucun système. |
| Routage | Content-Based Router | choisit le canal selon le contenu ; ne modifie pas le message. |
| | Message Filter | garde ou jette. |
| | Dynamic Router | règles mises à jour par les destinataires. |
| | Recipient List | liste de destinataires calculée. |
| | Splitter | un message devient n. |
| | Aggregator | n messages deviennent un. |
| | Resequencer | remet dans l'ordre (retient les messages). |
| | Scatter-Gather | diffuse, puis agrège les réponses. |
| | Routing Slip | l'itinéraire voyage avec le message. |
| | Process Manager (saga) | un état persisté par instance, orchestre commandes et réponses, compense. |
| Gestion | Control Bus | piloter les endpoints par messages. |
| | Detour | dérouter vers une étape optionnelle. |
| | Wire Tap | copier vers un canal d'audit sans perturber. |
| | Message History | la liste des étapes traversées. |
| | Message Store | une copie consultable (par corrélation). |
| | Smart Proxy | suivre les request-reply. |
| | Test Message | un message de sonde. |

Outillage .NET : `System.Threading.Channels` (intra-processus), MassTransit (choix Trame 2), Wolverine,
NServiceBus, `Azure.Messaging.ServiceBus`, `RabbitMQ.Client`, Dapr.

## La plateforme .NET en septembre 2026

| Version | Statut | Usage |
|---|---|---|
| .NET 10 (LTS, nov. 2025) | supporté jusqu'en nov. 2028, C# 14 | cible unique de tout nouveau développement |
| .NET 8 (LTS) et .NET 9 (STS) | fin de support nov. 2026 | migrer vers .NET 10 |
| .NET Framework 4.8.1 | maintenance : correctifs de sécurité, Windows uniquement | jamais pour du neuf ; Trame en attendant la migration |
| .NET Standard 2.0 | spécification figée | bibliothèques partagées Framework / .NET pendant la migration |

Ce qui n'existe plus : Web Forms, WCF serveur (CoreWCF pour migrer), MSMQ, COM+/DTC, Remoting, AppDomains,
`BinaryFormatter`, GAC, Linq to SQL, WIF. Le CLR : chargement à la demande, JIT tiered + PGO, GC générationnel,
ReadyToRun, NativeAOT. Transactions : `System.Transactions` local seulement. Message queuing : bibliothèques,
pas de runtime.

## Structurer et nommer

```text
Textinord.Trame.sln · Directory.Build.props · Directory.Packages.props · .editorconfig · global.json
src/  Domain (rien) ← Application ← Infrastructure ; Api et Worker assemblent
tests/ <Projet>.Tests référence uniquement le projet testé
```

| Élément | Règle | Exemple |
|---|---|---|
| Assembly = namespace racine | `Societe.Produit.Couche`, PascalCase | `Textinord.Trame.Application` |
| Types, méthodes, propriétés, constantes | PascalCase, sans abréviation, pas de hongrois | `OrdrePreparationTranslator`, `NombreMaximalDeLignes` |
| Interface | préfixe `I` | `IMessageBus` |
| Méthode asynchrone | suffixe `Async` | `PublierAsync` |
| Champ privé / paramètre / variable | `_camelCase` / `camelCase` | `_deadLetters`, `cancellationToken` |
| Projet de tests | `<Projet>.Tests`, classe `<Type>Tests`, méthode = comportement | `InProcessMessageBusTests` |

Règles dans `.editorconfig` en `warning` + `dotnet_diagnostic.IDE1006.severity = warning` +
`EnforceCodeStyleInBuild` + `TreatWarningsAsErrors` = une faute de nommage ne compile pas.

## Commandes

```bash
dotnet new sln -n Textinord.Trame --format sln
dotnet new classlib -n Textinord.Trame.Domain -o src/Textinord.Trame.Domain
dotnet sln add src/*/*.csproj tests/*/*.csproj
dotnet add src/Textinord.Trame.Application reference src/Textinord.Trame.Domain
dotnet build && dotnet test
dotnet format --verify-no-changes
```
