# Exercice 5.1 — Concevoir le contrat REST des commandes Textinord

> Module : 5 — Communication, API et Cloud Azure
> Durée estimée : 25 min
> Difficulté : 2 / 5
> Type : Exercice de conception en binôme, sur papier ou dans un fichier Markdown

## Objectifs pédagogiques

À la fin de cet exercice, vous serez capable de :

- Décomposer un besoin métier en ressources REST nommées, sans verbe dans les URL
- Associer à chaque opération le verbe HTTP, les codes de réponse et la forme d'erreur attendus
- Décider d'une stratégie de versioning, de pagination et de filtrage adaptée à la volumétrie de Textinord

## Prérequis

- Avoir suivi la section 3 du module (REST et Web API) et la démo 5.1
- Aucun outil requis ; un éditeur Markdown ou une feuille suffisent
- Avoir sous les yeux le modèle métier de `FIL-ROUGE.md` (Commande, LigneCommande, statuts, OrdrePreparation)

## Contexte

Julien Delcourt rédige l'ADR « API commandes de Trame 2 ». Il lui manque la pièce centrale :
le contrat, que Sofia Marques implémentera au TP 5 et que trois consommateurs attendent.

- L'**extranet Blazor** (clients professionnels) : consulte ses commandes, en crée, suit l'avancement.
- L'**application MAUI des scanners** (préparateurs de Roubaix et Lesquin) : lit les ordres de préparation d'un entrepôt, les clôt.
- Les **20 partenaires EDI** : créent des commandes en masse la nuit (jusqu'à 400 lignes par commande), interrogent leur statut, et rejouent parfois le même fichier deux fois.

Volumétrie : 900 commandes par jour en pointe, 3 000 lignes par jour, 10 ans d'archives à
conserver, 40 000 références. Objectif de disponibilité 99,9 %, P95 sous 300 ms sur la lecture.

Règles métier à respecter dans le contrat : une commande se valide seulement si chaque ligne
a du stock dans au moins un entrepôt, sinon elle passe `EnAttenteStock` ; un ordre de
préparation est émis par entrepôt concerné à la validation ; annulation possible avant
préparation ; numéro `CMD-AAAA-NNNNNN` attribué par le serveur.

## Énoncé

### Partie 1 — Les ressources (5 min)

Listez les ressources exposées par l'API, leur URL canonique et leur relation. Pour chacune,
indiquez qui la consomme (extranet, scanner, EDI) et si elle est en lecture seule pour ce
consommateur.

Résultat attendu : quatre à six ressources, dont au moins une sous-ressource (par exemple
les ordres de préparation d'une commande, ou les ordres d'un entrepôt).

### Partie 2 — Le tableau des opérations (12 min)

Remplissez le tableau suivant, une ligne par opération. Couvrez obligatoirement : créer une
commande, lire une commande, lister et filtrer les commandes d'un client, valider une
commande (avec le cas « stock indisponible »), annuler une commande (avec le cas « déjà en
préparation »), lister les ordres de préparation d'un entrepôt, clore un ordre de préparation.

| Opération | Verbe + URL | Corps de requête | Succès (code + corps) | Échecs (code + `type` ProblemDetails) |
|---|---|---|---|---|
| Créer une commande | | | | |

Pour chaque échec, écrivez l'URI `type` du ProblemDetails (par exemple
`https://trame.textinord.example/problemes/client-inconnu`) et le code HTTP. Distinguez
erreur de forme (400), règle métier sur la requête (422) et conflit d'état (409).

### Partie 3 — Versioning, pagination, idempotence (8 min)

Répondez en trois à cinq lignes chacune :

1. **Versioning** : où placez-vous la version (URL, en-tête, query string) et pourquoi, sachant que les partenaires EDI configurent leurs outils une fois pour dix-huit mois ? Quel changement du contrat oblige à passer en v2, lequel ne l'oblige pas ? Donnez un exemple concret pour la ressource commande (la remise expliquée par composante est un bon candidat).
2. **Pagination et filtres** : comment un client parcourt-il dix ans d'archives sans casser l'API ? Choisissez entre `page` + `taille` et un curseur, fixez un plafond, listez les filtres (statut, client, période) et le tri par défaut. Indiquez où vous renvoyez le total et le nombre de pages.
3. **Idempotence** : le partenaire « Aciéries de Denain » envoie deux fois le même fichier EDI de 80 commandes. Que se passe-t-il avec votre contrat ? Proposez le mécanisme (en-tête, clé, code de réponse en cas de rejeu) qui évite les doublons.

## Indices (à consulter si bloqué)

<details>
<summary>Indice 1 — Je ne sais pas comment exprimer « valider » en REST</summary>

Une transition d'état n'est pas un CRUD. Deux formes acceptables : un POST sur une
sous-ressource d'action (`POST /commandes/{numero}/validation`) ou une modification
explicite du statut (`PATCH /commandes/{numero}` avec `{"statut": "Validee"}`). La première
rend le contrat plus lisible et évite de laisser un client écrire n'importe quel statut.
Dans les deux cas, une transition interdite est un 409 avec un `type` stable.

</details>

<details>
<summary>Indice 2 — 400, 409 ou 422 ?</summary>

400 : la requête est mal formée (champ manquant, quantité négative, JSON invalide). 422 : la
requête est bien formée mais viole une règle qui la concerne elle-même (code client inconnu,
référence hors catalogue). 409 : la requête est correcte mais l'état actuel de la ressource
l'interdit (annuler une commande déjà en préparation, valider une commande annulée).

</details>

<details>
<summary>Indice 3 — Le rejeu EDI</summary>

Un en-tête `Idempotency-Key` porté par le client (le numéro du fichier EDI et la ligne, par
exemple) : le serveur mémorise la clé et la réponse pendant 24 h ; un rejeu avec la même clé
renvoie la réponse initiale (201 avec le même `Location`) au lieu de créer un doublon. Un
rejeu avec la même clé mais un corps différent est un 422.

</details>

## Pour aller plus loin (bonus)

Ajoutez une colonne « autorisation » au tableau : quel rôle (`adv`, `entrepot`, `partenaire`)
peut appeler chaque opération, et sur quelles données (un partenaire ne voit que ses
commandes). Puis écrivez, en une phrase chacun, les en-têtes de réponse que vous imposez sur
toutes les lectures : `api-supported-versions`, `ETag`, `Cache-Control`.
