# Exercice 2.1 — Choisir le pattern d'intégration

**Module 2 — Intégration par messages et plateforme .NET · 20-25 min · individuel puis binôme**
Slide de renvoi : 22. Supports utiles : slides 8 à 21 (canaux, construction, transformation, routage, gestion).

## Objectifs

- Reconnaître, dans une situation métier concrète, le pattern d'intégration qui la résout.
- Choisir la sémantique de canal adaptée (point-à-point ou publish-subscribe) et la justifier.
- Prendre l'habitude de nommer les patterns avec le vocabulaire de Hohpe et Woolf : c'est ce vocabulaire que
  vous retrouverez dans la documentation de MassTransit, d'Azure Service Bus ou de NServiceBus.

## Prérequis

- Avoir suivi les sections 1 à 3 du module (slides 4 à 21).
- La cheatsheet `cheatsheet/module-02.md` est autorisée.

## Contexte Textinord

Julien Delcourt, l'architecte, prépare l'ADR « intégration par messages » de Trame 2. Il a listé six flux qui
posent problème aujourd'hui dans Trame (WCF synchrone, MSMQ non supervisé, fichiers traités à la main) et vous
demande, pour chacun, une proposition argumentée. Il n'attend pas une seule bonne réponse : il attend un
pattern principal, une sémantique de canal et une phrase qui explique pourquoi.

## Énoncé

### Partie 1 — Les six situations (15 min)

Pour chaque situation, remplissez le tableau : pattern principal (un seul), patterns secondaires éventuels,
type de canal (point-à-point, publish-subscribe, ou autre canal spécialisé), et une phrase de justification.

| N° | Situation |
|---|---|
| 1 | **EDI des grands comptes.** Vingt clients déposent chaque nuit leurs commandes sur un SFTP, dans trois formats différents (EDIFACT, CSV maison, XML). Aujourd'hui un opérateur ADV les ressaisit dans Trame. Trame 2 doit les intégrer automatiquement dans le même flux que les commandes saisies à la main. |
| 2 | **Notification client.** À l'expédition d'une commande, le client doit être prévenu par e-mail ; certains veulent aussi un SMS, l'extranet doit afficher le statut, et la direction commerciale annonce déjà « un jour, WhatsApp ». Le service qui déclare l'expédition ne doit pas être modifié à chaque nouveau canal de notification. |
| 3 | **Ordre de préparation multi-entrepôt.** Une commande validée contient des lignes servies par Roubaix et d'autres par Lesquin. Chaque entrepôt doit recevoir uniquement ce qui le concerne, dans le format de son WMS, et savoir combien d'ordres composent la commande. |
| 4 | **Accusés des entrepôts.** Une commande est déclarée expédiée seulement quand tous ses ordres de préparation sont clos. Les entrepôts répondent dans un ordre quelconque, parfois à plusieurs heures d'intervalle. Marc Vandewalle veut être alerté si un ordre reste sans réponse plus de 48 heures. |
| 5 | **Commandes urgentes.** Une commande marquée « urgente » par l'ADV doit être préparée avant les autres, même si 300 ordres normaux attendent déjà. Le broker cible (Azure Service Bus) n'a pas de notion de priorité dans une file. |
| 6 | **Audit réglementaire.** Toutes les commandes échangées doivent être conservées dix ans (archivage légal) et consultables par numéro de commande, sans ralentir le flux de préparation ni modifier les consommateurs existants. Les bons de livraison PDF (jusqu'à 2 Mo) font partie de ce qu'il faut conserver. |

Tableau à remplir :

| N° | Pattern principal | Patterns secondaires | Type de canal | Justification (une phrase) |
|---|---|---|---|---|
| 1 | | | | |
| 2 | | | | |
| 3 | | | | |
| 4 | | | | |
| 5 | | | | |
| 6 | | | | |

### Partie 2 — Confrontation (5-10 min)

En binôme, comparez vos tableaux. Pour chaque ligne où vous divergez, décidez lequel des deux patterns est le
principal et lequel est secondaire ; notez le cas où les deux réponses sont réellement défendables.

<details>
<summary>Indice — situation 1</summary>

Trois formats en entrée, un seul format attendu en sortie : quel pattern de transformation est fait de
« un routeur plus un traducteur par format » ? Et comment un fichier sur un SFTP entre-t-il dans un système de
messages ?

</details>

<details>
<summary>Indice — situation 2</summary>

L'émetteur ne doit pas connaître ses destinataires. Relisez la slide 8 : quel canal permet d'ajouter un
abonné sans toucher à l'émetteur ? Le message est-il une commande ou un événement ?

</details>

<details>
<summary>Indice — situation 3</summary>

« Un message devient n messages » puis « chaque message est converti au format du destinataire ». Deux
patterns enchaînés, plus un en-tête de la slide 12 pour que l'entrepôt sache si la commande est complète.

</details>

<details>
<summary>Indice — situation 4</summary>

« n messages deviennent un » : lequel ? Et quand il faut en plus gérer un délai et une alerte, quel pattern de
la slide 18 prend le relais ?

</details>

<details>
<summary>Indice — situation 5</summary>

Si le broker ne sait pas prioriser dans une file, comment obtenir la priorité avec deux files ? Quel pattern
décide dans quelle file va le message ?

</details>

<details>
<summary>Indice — situation 6</summary>

« Sans modifier les consommateurs » et « sans ralentir » : slide 19, le pattern qui copie sans perturber, et
celui qui conserve. Pour les PDF de 2 Mo : slide 13.

</details>

## Bonus

Dessinez sur une feuille le pipeline complet qui relie les six situations : canaux nommés (`commandes.recues`,
`commandes.validees`, `preparations.*`, `entrepot.*`, `audit`…), filtres, dead letter. Vous pouvez partir du
schéma de la slide 16 et le compléter. Indiquez pour chaque canal s'il est point-à-point ou publish-subscribe.
