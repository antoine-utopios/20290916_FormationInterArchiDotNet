# Solution — Exercice 4.1 : Choisir le stockage de six données Textinord

> Document formateur — Ne pas distribuer avant la fin de l'exercice.

## Approche pédagogique

Il n'y a pas une réponse unique par ligne : on évalue la démarche (une question posée à la donnée, une famille qui en découle, une perte assumée), pas la marque du moteur. Le piège volontaire est le stock temps réel : il oblige à séparer « rapide » et « juste », et donc à découvrir qu'une donnée peut avoir une source de vérité et un chemin de lecture différents. Pendant l'exercice, repérez les binômes qui justifient par les qualités du moteur (« Cosmos DB est scalable ») et ramenez-les à la donnée (« qui lit quoi, à quelle fréquence, avec quelle exigence de cohérence ? »).

## Solution détaillée

### Partie 1 — Le tableau de référence

| Donnée | Famille | Moteur Azure | Justification | Ce que l'on perd |
|---|---|---|---|---|
| Commandes et lignes | Relationnel | Azure SQL Database (EF Core 10) | transactions multi-tables (commande + lignes + stock) ; jointures pour la recherche par client, statut, période ; contraintes d'intégrité ; archivage 10 ans avec temporal tables ou partitionnement | scalabilité horizontale en écriture (inutile à 900 commandes / jour) ; coût au vCore même la nuit (Azure SQL serverless le mitige) |
| Catalogue produit | Document | Azure Cosmos DB (API NoSQL), partition `/famille`, cohérence Session | 200 000 lectures / jour de fiches entières ; attributs variables par famille (taille, grammage, norme EPI) sans table d'attributs générique ; copie publiée depuis la saisie ADV en SQL | pas de jointure (le prix négocié par client reste en SQL) ; double stockage à synchroniser (événement `ArticlePublie`) ; compétence à acquérir dans l'équipe de Sofia |
| Panier de l'extranet | Clé-valeur | Azure Cache for Redis (hash par client, TTL 7 jours) | donnée jetable et reconstructible ; lectures / écritures par clé ; expiration native ; latence | pas de requête « tous les paniers contenant l'article X » sans index applicatif ; perte en cas de purge du cache (acceptée) |
| Sessions utilisateur | Clé-valeur | Redis comme backplane SignalR ; état de circuit Blazor en mémoire | Blazor InteractiveServer garde l'état dans le circuit ; plusieurs instances Container Apps exigent un backplane pour SignalR et l'affinité de session ; ce qui doit survivre à une instance va dans Redis avec TTL | reconnexion perdue si l'instance disparaît (le circuit ne se restaure pas) ; ce point pousse à garder l'état métier hors du circuit |
| Journal d'audit | Relationnel puis archive | Azure SQL (temporal table sur `Commandes`, ou table `AuditCommande` en append-only), archivage vers Blob Storage (Parquet ou JSON lines) après 12 mois | traçabilité liée à la transaction qui modifie la commande (même commit) ; consultation rare par numéro ; conservation 10 ans à coût maîtrisé hors de la base transactionnelle | requêtes analytiques sur l'archive plus lentes (Synapse ou Fabric si besoin) ; deux emplacements à documenter |
| Stock temps réel | Relationnel (source de vérité) + clé-valeur (lecture) | Azure SQL pour les mouvements et la quantité courante ; Redis devant, TTL 30 s, invalidé par événement `StockModifie` | la validation de commande lit SQL sous transaction (cohérence forte) ; la consultation scanner / extranet lit Redis (P95 < 300 ms) ; 15 000 mouvements / jour tiennent dans SQL sans discussion | un affichage peut avoir jusqu'à 30 s de retard (accepté pour la consultation, jamais pour la validation) ; une invalidation à écrire et à tester |

Une réponse « Cosmos DB pour le stock » ou « Redis comme source de vérité du stock » est à refuser explicitement : c'est le scénario de la commande promise deux fois (slide 10).

### Partie 2 — Le piège assumé

Deux données admettent deux réponses défendables :

**Stock temps réel.** Réponse A : tout en Azure SQL, avec un index adapté et RCSI ; à 15 000 mouvements / jour et quelques centaines de consultations par minute, SQL tient le P95 < 300 ms sans cache. Réponse B : SQL + Redis. Question qui tranche, pour Nadia Benali : « Acceptez-vous qu'un préparateur voie un stock vieux de 30 secondes sur son scanner, si en échange la validation d'une commande reste toujours juste ? » Si non, réponse A avec un budget de base plus élevé ; si oui, réponse B.

**Catalogue.** Réponse A : tout en Azure SQL, attributs variables dans une colonne JSON (`json` natif SQL Server 2025), et un cache Redis devant l'extranet. Réponse B : Cosmos DB en copie publiée. Question qui tranche : « Voulez-vous que l'extranet reste disponible et rapide même si la base des commandes est saturée ou en maintenance ? » Si oui, la copie publiée dans un autre moteur se justifie ; sinon, la colonne JSON dans SQL suffit et l'équipe garde un seul moteur à exploiter.

Les deux réponses sont bonnes si la question de décision est formulée en termes de conséquence métier, pas en termes techniques.

### Partie 3 — Restitution croisée : ce qu'on entend souvent

- « Cosmos DB parce que c'est du NoSQL moderne » : justification par le moteur, à reformuler à partir du besoin (lecture entière, schéma variable, disponibilité).
- « Redis pour les commandes parce que c'est rapide » : confusion entre latence de lecture et besoin transactionnel ; les commandes s'écrivent, se joignent et s'archivent.
- Colonne « ce que l'on perd » vide : demander « que se passe-t-il si ce moteur est indisponible dix minutes ? » : la réponse remplit la colonne.

## Variantes acceptables

1. Journal d'audit dans Cosmos DB (append-only, TTL par document pour l'archivage) : acceptable si le binôme explique comment il garantit que l'audit est écrit dans la même transaction que la modification (Outbox, module 6). Sans cette réponse, la variante crée une incohérence possible entre la commande et sa trace.
2. Sessions « tout en Redis » avec Blazor en mode WebAssembly ou Auto : acceptable, le raisonnement est le même (l'état qui survit à l'instance va dans Redis).
3. Catalogue en PostgreSQL `jsonb` : acceptable techniquement ; à confronter à la contrainte de compétences (six développeurs SQL Server) et de coexistence avec Trame.

## Erreurs classiques à repérer en correction

| Erreur observée | Cause probable | Comment corriger |
|---|---|---|
| Un moteur différent par ligne (six moteurs) | on répond « quel est le meilleur outil » au lieu de « quel est le plus petit nombre d'outils nécessaires » | rappeler la règle SQL par défaut et le coût d'exploitation de chaque moteur supplémentaire (compétence, sauvegarde, supervision, sécurité) |
| Stock temps réel dans Redis uniquement | confusion entre cache et source de vérité | rejouer le scénario de la commande promise deux fois |
| Catalogue en SQL avec 40 colonnes nullables | on force le schéma variable dans le relationnel | montrer la colonne JSON ou le document ; demander combien de colonnes seront nulles pour un drap |
| Justification en une ligne « performance » | pas de chiffre | demander le volume et la fréquence, ils sont dans `FIL-ROUGE.md` |

## Points à insister en débriefing

- Une donnée peut avoir une source de vérité (SQL) et un chemin de lecture optimisé (Redis, Cosmos DB en copie) : ce n'est pas une contradiction, c'est CQRS à petite échelle, à condition de savoir invalider.
- « Ce que l'on perd » est la colonne qui transforme un choix en décision : sans elle, l'ADR ne pourra pas être relue dans un an.
- Le TP 4 implémente la première ligne (commandes en SQL avec EF Core 10) ; le module 5 exposera le stock à l'API avec le cache, le module 6 traitera l'événement d'invalidation.

## Bonus

ADR attendue pour le catalogue : contexte (200 000 lectures / jour, attributs variables, extranet disponible même en maintenance SQL), décision (copie publiée dans Cosmos DB, partition `/famille`, cohérence Session, publication par événement), conséquences (double stockage, synchronisation à superviser, compétence à acquérir, coût en RU/s à mesurer), date de revue : six mois après la mise en production de l'extranet, avec la mesure réelle des RU consommées et du délai de publication.
