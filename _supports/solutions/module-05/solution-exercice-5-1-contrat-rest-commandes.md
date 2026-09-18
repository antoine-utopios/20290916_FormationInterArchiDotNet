# Solution — Exercice 5.1 : Concevoir le contrat REST des commandes Textinord

> Document formateur — Ne pas distribuer avant la correction collective.

## Approche pédagogique

L'exercice est un exercice de conception : il n'y a pas une seule bonne réponse, mais il y a
des réponses fausses (verbes dans les URL, 200 pour tout, une erreur libre par endpoint, pas
de version). La correction se fait au tableau en reconstituant le tableau ci-dessous ligne par
ligne, en demandant à un binôme différent chaque ligne. Insistez sur trois points : la
distinction 400 / 409 / 422, la stabilité du champ `type` des ProblemDetails, et le fait que
le contrat de la v1 ne bouge plus une fois publié aux partenaires EDI. Ce tableau est
exactement ce que le corrigé du TP 5 implémente (à l'exception des ordres de préparation,
qui attendent le module 6 et le bus).

## Solution détaillée

### Partie 1 — Les ressources

| Ressource | URL canonique | Consommateurs | Notes |
|---|---|---|---|
| Commande | `/api/v1/commandes/{numero}` | extranet (ses commandes), scanner (lecture), EDI (ses commandes) | agrégat racine ; `numero` = `CMD-AAAA-NNNNNN` attribué par le serveur |
| Collection des commandes | `/api/v1/commandes` | idem | paginée, filtrable par `statut`, `client`, `depuis`, `jusqua` |
| Lignes d'une commande | incluses dans la représentation de la commande | idem | pas d'URL propre : une ligne ne vit pas sans sa commande (agrégat) |
| Ordres de préparation d'une commande | `/api/v1/commandes/{numero}/ordres-preparation` | extranet (suivi), ADV | lecture seule ; créés par la validation |
| Ordres de préparation d'un entrepôt | `/api/v1/entrepots/{code}/ordres-preparation` | scanners (Roubaix `RBX`, Lesquin `LSQ`) | la file de travail de Marc Vandewalle ; filtre `statut=AFaire` |
| Ordre de préparation | `/api/v1/ordres-preparation/{id}` | scanners | clôture via une sous-ressource d'action |
| Référentiel des statuts | `/api/v1/referentiel/statuts` | tous | public, mis en cache, pour afficher les libellés |

Décisions à défendre en correction : la ligne de commande n'a pas d'URL (on modifie une
commande en brouillon en renvoyant ses lignes, ou en v2 par `PATCH`) ; l'ordre de préparation
a deux chemins d'accès (par commande et par entrepôt) mais une seule identité.

### Partie 2 — Le tableau des opérations

| Opération | Verbe + URL | Corps de requête | Succès | Échecs (code + `type`) |
|---|---|---|---|---|
| Créer une commande | `POST /api/v1/commandes` | `{ codeClient, lignes: [{ reference, quantite, prixNegocie? }] }` | 201, `Location: /api/v1/commandes/CMD-2026-004512`, corps = commande | 400 `…/requete-invalide` (validation de forme : champ manquant, quantité ≤ 0, plus de 200 lignes) ; 422 `…/client-inconnu`, `…/article-inconnu` ; 401 / 403 |
| Lire une commande | `GET /api/v1/commandes/{numero}` | — | 200, corps = commande (lignes, taux de remise, montant net, entrepôt affecté) | 404 `…/commande-introuvable` ; 403 si la commande appartient à un autre client (partenaire) |
| Lister les commandes d'un client | `GET /api/v1/commandes?client=CLI-0311&statut=Validee&page=2&taille=50` | — | 200, `{ elements: [...], numero: 2, taille: 50, total: 812, nombrePages: 17 }` | 400 `…/requete-invalide` (statut inconnu, taille > 100) |
| Valider une commande | `POST /api/v1/commandes/{numero}/validation` | vide | 200, corps = commande en `Validee` (entrepôt affecté par ligne) **ou** en `EnAttenteStock` (aucune ligne affectée) | 404 ; 409 `…/transition-interdite` (déjà validée, annulée, facturée) ; 409 `…/commande-vide` |
| Annuler une commande | `DELETE /api/v1/commandes/{numero}` | — | 204 | 404 ; 409 `…/transition-interdite` (déjà en préparation ou au-delà) |
| Lister les ordres d'un entrepôt | `GET /api/v1/entrepots/LSQ/ordres-preparation?statut=AFaire&page=1&taille=100` | — | 200, page d'ordres (commande, lignes à préparer, préparateur) | 404 `…/entrepot-inconnu` ; 403 pour un partenaire |
| Clore un ordre de préparation | `POST /api/v1/ordres-preparation/{id}/cloture` | `{ preparateur, quantitesPreparees: [{ ligne, quantite }] }` | 200, corps = ordre en `Clos` ; si tous les ordres de la commande sont clos, la commande passe `Expediee` | 404 ; 409 `…/ordre-deja-clos` ; 422 `…/quantite-incoherente` |

Points à faire ressortir :

- **Validation** est un `POST` sur une sous-ressource d'action, pas un `PATCH` du statut : le
  client n'a pas à connaître la machine à états, et le résultat (`Validee` ou
  `EnAttenteStock`) est décidé par le serveur. Réponse 200 (pas 201 : rien n'est créé du
  point de vue du client, même si des ordres de préparation naissent côté serveur ; on peut
  accepter 202 si la validation devient asynchrone au module 6).
- **Annuler** est un `DELETE` logique : la ressource existe toujours, en `Annulee`. C'est
  acceptable en REST tant que le contrat le dit ; l'alternative `POST …/annulation` est aussi
  correcte. Ce qui n'est pas acceptable : `GET /commandes/annuler?numero=…`.
- Les échecs 4xx portent tous un ProblemDetails dont le `type` est une URI stable sous
  `https://trame.textinord.example/problemes/`, un `title` humain, un `detail` contextuel,
  et une extension `code` (`TRANSITION_INTERDITE`, `CLIENT_INCONNU`) reprise du pattern Result.

### Partie 3 — Versioning, pagination, idempotence

**1. Versioning.** Version dans le segment d'URL (`/api/v1/…`) : elle est visible dans les
logs, les caches et les configurations des partenaires EDI, qui ne savent souvent pas
positionner un en-tête personnalisé. L'en-tête `api-supported-versions` accompagne chaque
réponse. Compatible (pas de nouvelle version) : ajouter un champ optionnel en réponse
(`entrepotAffecte`), ajouter un filtre, ajouter un endpoint. Incompatible (v2 obligatoire) :
renommer ou retirer un champ, changer un type, rendre obligatoire un champ de requête. Exemple
concret : en v1, une ligne porte `tauxRemise` (un nombre) ; en v2, elle porte `remise` avec
`client`, `volume`, `appliquee`, `plafondAtteint`, et la commande porte `totaux` décomposés.
La v1 reste servie dix-huit mois (durée de vie des postes WinForms et des configurations
EDI), avec un en-tête `Sunset` annonçant la date de retrait six mois avant.

**2. Pagination et filtres.** `page` + `taille` (défaut 20, plafond 100) pour l'extranet et
les scanners : simple, adapté à une interface avec numéros de page. Pour les partenaires EDI
qui synchronisent dix ans d'archives, un curseur (`?apres=CMD-2024-118203&taille=100`) évite
les pages qui bougent quand des commandes s'insèrent ; on peut l'introduire en v1 comme
filtre optionnel sans casser le contrat. Filtres : `statut`, `client`, `depuis`, `jusqua`
(dates ISO 8601), tri par défaut `numero` décroissant (le plus récent d'abord), `tri=date:asc`
en option. Le total et le nombre de pages sont dans le corps (`total`, `nombrePages`), pas
dans des en-têtes, pour que les clients générés par Kiota les voient dans le schéma.

**3. Idempotence.** Sans mécanisme, les 80 commandes du second fichier sont créées une
deuxième fois avec de nouveaux numéros : 160 commandes, 80 doublons, des ordres de
préparation en double pour Lesquin. Mécanisme : un en-tête `Idempotency-Key` obligatoire pour
les partenaires (valeur : `EDI-<partenaire>-<numéro de fichier>-<ligne>`), conservé 24 h avec
la réponse initiale. Rejeu avec la même clé et le même corps : le serveur renvoie la réponse
d'origine (201, même `Location`, même numéro) sans rien créer. Même clé avec un corps
différent : 422 `…/cle-idempotence-reutilisee`. Alternative plus simple si l'EDI porte déjà un
identifiant métier unique : accepter un champ `referenceExterne` dans la requête et répondre
409 `…/commande-existante` avec le numéro déjà attribué.

## Variantes acceptables

1. `PATCH /commandes/{numero}` avec `{ "statut": "Validee" }` à la place de la sous-ressource
   d'action : correct si le contrat liste précisément les transitions autorisées et répond
   409 sur les autres. Faire remarquer que la lecture du contrat devient moins évidente.
2. 202 Accepted sur la validation si l'équipe anticipe une validation asynchrone (émission
   des ordres par le bus au module 6) : correct, à condition de fournir une URL de suivi.
3. Version dans un en-tête `api-version` : acceptable pour un usage interne, mais faire
   argumenter le cas des partenaires EDI et des caches HTTP.
4. Une ressource `lignes` sous la commande avec `PUT /commandes/{numero}/lignes` pour
   remplacer les lignes d'un brouillon : correct et utile pour l'extranet.

## Erreurs classiques à repérer en correction

| Erreur observée | Cause probable | Comment corriger |
|---|---|---|
| `POST /commandes/valider`, `GET /commandes/annuler` | réflexe RPC hérité de WCF | ressource + verbe ; l'action devient une sous-ressource |
| 200 sur la création, 200 avec `{ "erreur": "…" }` sur un échec | habitude des services SOAP qui répondent toujours 200 | 201 + `Location` ; erreurs en 4xx ProblemDetails |
| Tout échec en 400 | pas de distinction forme / règle / état | 400 forme, 422 règle sur la requête, 409 état de la ressource |
| Version absente « on verra plus tard » | sous-estimation du coût de changement chez 20 partenaires | version dans l'URL dès la v1 ; sans version, la première évolution casse tout le monde |
| Pagination sans plafond | pas de vision de la volumétrie (10 ans, 900 par jour) | plafond 100, total dans le corps, curseur pour les gros volumes |
| Le partenaire voit toutes les commandes | autorisation pensée par endpoint, pas par donnée | filtre implicite par client dans le handler (claim `client` du jeton) : 403 ou 404 sur une commande d'un autre client |

## Points à insister en débriefing

- Le contrat se lit sans le code : verbes, codes et `type` d'erreur suffisent à écrire le
  client. C'est ce que fournira OpenAPI automatiquement au TP 5, à condition que les
  `TypedResults` et les `ProducesProblem` soient déclarés.
- La v1 est un engagement : dix-huit mois de compatibilité pour Textinord. Toute évolution
  passe par « ajouter », jamais par « modifier ».
- L'idempotence n'est pas un luxe : le fichier EDI rejoué est le premier incident que Sofia
  aura en production. Le rejeu doit être ennuyeux, pas catastrophique.

## Bonus

Colonne autorisation attendue : lecture (`commandes.lecture`) pour `adv`, `entrepot`,
`partenaire` avec un filtre par client pour `partenaire` ; écriture (`commandes.ecriture`,
création, validation, annulation) pour `adv` et `partenaire` (ses propres commandes
uniquement, sans validation) ; ordres de préparation en lecture et clôture pour `entrepot` sur
son propre entrepôt (claim `site`). En-têtes de réponse sur toutes les lectures :
`api-supported-versions: 1.0, 2.0` (versions servies), `ETag` (hash de la représentation, pour
`If-None-Match` et `If-Match`), `Cache-Control: private, max-age=0` sur les commandes et
`public, max-age=300` sur le référentiel.
