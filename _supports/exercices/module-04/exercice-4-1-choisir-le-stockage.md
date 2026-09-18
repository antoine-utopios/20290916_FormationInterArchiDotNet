# Exercice 4.1 — Choisir le stockage de six données Textinord

> Module : 4 — Persistance : SQL, NoSQL, ADO.NET et EF Core
> Durée estimée : 20 min (15 min de travail, 5 min de restitution croisée)
> Difficulté : 2 / 5
> Type : Exercice d'analyse en binôme, sans code

## Objectifs pédagogiques

À la fin de cet exercice, vous serez capable de :

- Rattacher une donnée d'entreprise à une famille de stockage (relationnel, document, clé-valeur, colonnes larges, graphe) à partir de critères explicites
- Nommer le moteur Azure correspondant et la façon d'y accéder depuis .NET 10
- Dire ce que l'on perd avec chaque choix, pas seulement ce que l'on gagne
- Rédiger la décision sous la forme d'un tableau qui pourra devenir une ADR

## Prérequis

- Avoir suivi les sections 1 à 2 du module 4 (slides 4 à 12 : bases épaisses, cinq modèles, CAP / PACELC, grille de choix)
- Avoir sous les yeux `FIL-ROUGE.md` pour la volumétrie de Textinord
- Aucun outil : papier, tableau blanc ou un fichier Markdown

## Contexte

Julien Delcourt, architecte de Trame 2, prépare l'ADR « stockage ». Il a reçu de Nadia Benali (DSI) une consigne claire : « un moteur par besoin, pas un moteur par développeur », et de Marc Vandewalle (entrepôt de Lesquin) une exigence de terrain : « quand un préparateur lit un stock sur son scanner, il doit être juste ». Sofia Marques ajoute qu'elle a six développeurs qui connaissent SQL Server et aucun qui a déjà exploité Cosmos DB.

Rappel des volumes : 900 commandes / jour en pointe, 3 000 lignes / jour, 40 000 articles, 15 000 mouvements de stock / jour, 1 200 clients, archivage des commandes pendant 10 ans, API à 99,9 % de disponibilité et P95 < 300 ms sur la consultation de stock.

## Énoncé

### Partie 1 — Placer les six données (10 min)

Pour chacune des six données ci-dessous, remplissez une ligne du tableau : famille de stockage, moteur Azure retenu, justification en trois lignes maximum, et ce que l'on perd (au moins un point).

| Donnée | Ce qu'on en fait | Volume et contraintes |
|---|---|---|
| Commandes et lignes | création, validation, préparation, facturation, recherche par numéro, client, statut, période | 900 / jour, 10 ans d'archive, transactions multi-tables |
| Catalogue produit | fiches lues entières par l'extranet Blazor, attributs qui varient selon la famille (taille, grammage, norme EPI, coloris) | 40 000 références, 200 000 lectures / jour, quelques dizaines de modifications / jour |
| Panier de l'extranet | ajout, retrait, abandon ; un client peut revenir le lendemain | 1 200 clients, données reconstructibles |
| Sessions utilisateur | Blazor Web App en mode InteractiveServer sur plusieurs instances Container Apps | quelques centaines de sessions simultanées |
| Journal d'audit | qui a changé quoi, quand, sur une commande ; consultation rare, conservation longue | 20 000 événements / jour, conservation 10 ans |
| Stock temps réel | lu par l'API et le scanner MAUI ; alimenté par 15 000 mouvements / jour ; sert à la règle de validation des commandes | P95 < 300 ms, doit être juste au moment de la validation |

Format attendu :

```text
| Donnée | Famille | Moteur Azure | Justification (3 lignes max) | Ce que l'on perd |
|---|---|---|---|---|
| ... | ... | ... | ... | ... |
```

### Partie 2 — Le piège assumé (3 min)

Au moins une de ces données admet deux réponses défendables. Identifiez-la, écrivez les deux réponses côte à côte et la question qui permet de trancher entre les deux (une seule question, formulée pour que Nadia Benali puisse y répondre sans compétence technique).

### Partie 3 — Restitution croisée (5 min)

Échangez votre tableau avec le binôme voisin. Chacun relève dans le tableau de l'autre :

- une ligne où la justification décrit un avantage du moteur plutôt qu'un besoin de la donnée (« Cosmos DB est scalable » n'est pas une justification) ;
- une ligne où la colonne « ce que l'on perd » est vide ou vague.

Résultat attendu : un tableau de six lignes, chacune avec une justification tirée du besoin et une perte assumée, plus une question de décision pour la donnée ambiguë.

## Indices (à consulter si bloqué)

<details>
<summary>Indice 1 — Je ne sais pas par où commencer</summary>

Posez les quatre questions de la slide 11 dans l'ordre, pour chaque donnée : faut-il des transactions et des jointures ? lit-on toujours l'objet entier avec un schéma variable ? peut-on la perdre et la recalculer ? le volume ou les relations dépassent-ils ce que SQL fait bien ? La première question qui reçoit « oui » désigne la famille.

</details>

<details>
<summary>Indice 2 — Le stock temps réel me semble contradictoire</summary>

Il l'est, et c'est voulu : « rapide » et « juste » ne se stockent pas forcément au même endroit. Demandez-vous qui a besoin de « rapide » (la consultation) et qui a besoin de « juste » (la validation de commande), puis s'il peut y avoir deux chemins de lecture pour une seule source de vérité.

</details>

<details>
<summary>Indice 3 — Le catalogue : SQL ou document ?</summary>

Regardez le rapport lectures / écritures et la variabilité des attributs par famille. Puis demandez-vous d'où viendraient les données du document : qui les saisit, dans quel outil, et si cette saisie a besoin de transactions.

</details>

## Pour aller plus loin (bonus)

Rédigez l'ADR correspondant à la ligne « catalogue » selon le gabarit du module 1 (contexte, décision, conséquences, date de revue). Ajoutez une conséquence négative que vous acceptez explicitement et la date à laquelle vous la réévaluerez.
