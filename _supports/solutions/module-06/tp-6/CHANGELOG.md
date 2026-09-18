# Journal des modifications — Trame 2

Toutes les évolutions notables de Trame 2 sont consignées ici, à destination des
métiers (ADV, entrepôts) autant que de l'équipe. Format inspiré de
[Keep a Changelog](https://keepachangelog.com/fr/1.1.0/) ; numérotation
[SemVer 2.0.0](https://semver.org/lang/fr/) calculée par Nerdbank.GitVersioning
(`version.json` fixe majeur.mineur, le correctif est la hauteur Git).

Règle d'écriture : une ligne par changement visible, au passé composé, avec
l'identifiant du work item Azure Boards entre crochets. Les changements internes
(refactoring, dépendances) vont dans « Technique ».

## [Non publié] — 2.1.0-beta

### Ajouté
- Relais Outbox du Worker : les événements `CommandeValidee` sont publiés sur le bus
  après validation en base, avec reprise automatique (10 tentatives) [#412].
- Consumer `OrdrePreparationConsumer` idempotent : un ordre par entrepôt, jamais de
  doublon en cas de rejeu du message [#413].
- Transport RabbitMQ activable par configuration (`Messaging:Transport = RabbitMq`) [#415].
- Endpoint de diagnostic `GET /diagnostic/outbox` pour la supervision [#418].

### Modifié
- `POST /commandes` écrit la commande et son message Outbox dans une seule transaction
  SQL : plus aucune commande validée sans ordre de préparation [#412].

### Technique
- Pipeline multi-étapes Azure DevOps (build, test, publish, recette, production sous
  approbation) et workflow GitHub Actions équivalent [#420].
- Versioning SemVer automatique (Nerdbank.GitVersioning), Central Package Management.

## [2.0.0] — 2026-08-28

### Ajouté
- API Trame 2 (`/commandes`, `/commandes/{numero}`) en ASP.NET Core 10 Minimal API
  avec OpenAPI [#301].
- Noyau métier `Textinord.Trame.Domain` : règle de remise (client + volume, plafond 30 %),
  cycle de vie de la commande, numérotation `CMD-AAAA-NNNNNN` [#288].

### Rupture
- Les postes WinForms ne consomment plus les services WCF `CommandeService.svc` :
  ils passent par la façade YARP (`/api/commandes`) [#295]. Les 18 mois de
  compatibilité courent jusqu'au 28/02/2028.

### Retiré
- File MSMQ `.\private$\ordres_preparation` : remplacée par le bus (topic `commandes`) [#296].

## [1.14.2] — 2026-06-12 (Trame, maintenance)

### Corrigé
- Perte d'ordres de préparation MSMQ au redémarrage du serveur de Lesquin :
  file rendue transactionnelle en attendant Trame 2 [#251].

[Non publié]: https://dev.azure.com/textinord/Trame2/_git/trame2/branches?baseVersion=GTv2.0.0&targetVersion=GBmain
[2.0.0]: https://dev.azure.com/textinord/Trame2/_git/trame2?version=GTv2.0.0
[1.14.2]: https://dev.azure.com/textinord/Trame/_git/trame?version=GTv1.14.2
