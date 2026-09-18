# Exercice 1.1 — Choisir un style d'architecture pour cinq besoins Textinord

> Module : 1 — Architecturer un SI .NET : styles, couches et design patterns
> Durée estimée : 25 min (15 min de travail, 10 min de restitution)
> Difficulté : 2 / 5
> Type : exercice d'analyse, en binôme

## Objectifs pédagogiques

À la fin de cet exercice, vous serez capable de :

- Associer un besoin métier à un style d'architecture (ou à une combinaison) en le justifiant par des critères, pas par des habitudes
- Utiliser la grille des qualités (maintenabilité, performance, sécurité, fiabilité, évolutivité, observabilité, coût, réversibilité) comme outil de décision
- Repérer quand un style « à la mode » ne répond pas au besoin

## Prérequis

- Avoir suivi les sections 1 et 2 du module (critères d'une bonne architecture, styles, couches)
- Aucun outil : papier, tableau blanc ou un fichier Markdown partagé dans le binôme

## Contexte

Julien Delcourt, architecte de Textinord, prépare le comité de septembre.
Nadia Benali (DSI) lui a demandé une réponse courte et argumentée pour chacun
des cinq besoins ci-dessous, tous inscrits au programme Trame 2. Les
contraintes de l'entreprise sont celles du fil rouge : 6 développeurs .NET,
900 commandes par jour en pointe, 1 200 clients professionnels, 20 grands
comptes en EDI, deux entrepôts, disponibilité visée 99,9 %, temps de réponse
P95 sous 300 ms sur la consultation de stock, compatibilité pendant 18 mois
avec les postes WinForms restants, budget PaaS Azure sans équipe d'exploitation
dédiée.

Les styles vus en cours, à combiner si nécessaire : 2-tiers, 3-tiers / n-tiers,
SOA / ESB / web services, WOA / REST, micro-services, monolithe modulaire,
event-driven, clean / hexagonale (organisation interne), vertical slices.

## Énoncé

### Partie 1 — Cinq besoins, cinq décisions (15 min)

Pour chacun des besoins suivants, écrivez : le style retenu (ou la
combinaison), puis **trois lignes de justification** qui citent au moins deux
critères de la grille et une contrainte Textinord chiffrée.

**Besoin A — L'extranet clients.** Les 1 200 clients professionnels
consultent le catalogue (40 000 références), composent un panier, passent
commande et suivent leurs expéditions. Pic de charge à la rentrée de
septembre. Les clients se connectent avec leur propre identité (Entra
External ID). L'extranet doit être en ligne avant que Trame ne soit éteint.

**Besoin B — L'application des préparateurs d'entrepôt.** Marc Vandewalle
(Lesquin) veut que ses préparateurs reçoivent les ordres de préparation sur
un scanner Android, y compris quand le Wi-Fi de l'entrepôt décroche entre
deux allées. Un ordre pris par un préparateur ne doit pas être repris par un
autre. 15 000 mouvements de stock par jour.

**Besoin C — L'EDI avec les 20 grands comptes.** Chaque grand compte envoie
ses commandes dans son propre format de fichier (certains contrats datent de
2009). Les fichiers arrivent par SFTP, à des heures variables. Une commande
EDI mal intégrée est une pénalité contractuelle. Les formats changent
rarement, mais chacun change indépendamment des autres.

**Besoin D — Le reporting de la direction.** Nadia Benali et la direction
commerciale veulent un tableau de bord : chiffre d'affaires par famille
d'articles, taux de service par entrepôt, commandes en attente de stock,
sur les 10 dernières années (archivage légal). Aucune écriture, des
requêtes lourdes, une consultation quotidienne le matin.

**Besoin E — La notification des clients.** À chaque changement de statut
d'une commande (validée, expédiée, facturée), le client reçoit un email ou
une notification. L'envoi ne doit jamais ralentir la validation de la
commande, ni la faire échouer si le fournisseur d'emails est en panne.

### Partie 2 — Le piège (inclus dans les 15 min)

Un des cinq besoins ci-dessus est celui pour lequel une équipe pressée
proposerait « des micro-services » en premier. Identifiez-le, et écrivez en
deux lignes pourquoi ce n'est pas la bonne réponse pour Textinord en 2026 —
et à quelle condition cela le deviendrait.

### Partie 3 — Restitution (10 min)

Chaque binôme présente un besoin (tiré au sort). Les autres binômes
contestent avec la grille des critères. Le formateur joue Julien Delcourt :
il ne valide qu'une justification qui cite un critère et une contrainte.

## Indices (à consulter si bloqué)

<details>
<summary>Indice 1 — Je ne sais pas par quel critère commencer</summary>

Posez d'abord la question de la **fiabilité** (que se passe-t-il si ce
composant tombe ?) puis celle du **coût** (qui exploite ça la nuit ?). Ces
deux critères éliminent la moitié des options pour chaque besoin.

</details>

<details>
<summary>Indice 2 — Plusieurs styles semblent convenir</summary>

C'est normal : les styles se combinent. Un extranet est à la fois du
n-tiers (présentation, API, données) et du REST (le contrat entre les
deux). Nommez la combinaison et dites quel style répond à quel critère.

</details>

<details>
<summary>Indice 3 — Le besoin E</summary>

Relisez « ne doit jamais ralentir la validation ». Un appel synchrone à un
fournisseur d'emails pendant la validation viole cette phrase par
construction. Quel style découple dans le temps l'émetteur du récepteur ?

</details>

## Pour aller plus loin (bonus)

Pour le besoin B, décrivez en cinq lignes ce qui se passe quand le scanner
retrouve le réseau après trois minutes de coupure : quelles données ont été
saisies hors ligne, comment elles remontent, et ce qui arrive si deux
préparateurs ont pris le même ordre pendant la coupure. Vous retrouverez ce
scénario au module 3 (.NET MAUI) et au module 4 (concurrence).
