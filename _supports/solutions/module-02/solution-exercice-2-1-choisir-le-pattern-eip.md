# Solution — Exercice 2.1 : choisir le pattern d'intégration

Document formateur. Les réponses « défendables » sont signalées : l'objectif n'est pas de trouver le mot exact
mais de justifier le choix avec les critères du module (couplage, sémantique de canal, ce qui est modifié
quand le besoin change).

## Tableau corrigé

| N° | Pattern principal | Patterns secondaires | Type de canal | Justification |
|---|---|---|---|---|
| 1 | **Normalizer** (un routeur par format + un traducteur par format vers le modèle canonique) | Channel Adapter (fichier SFTP → message), Canonical Data Model, Invalid Message Channel pour les fichiers illisibles, Splitter si un fichier contient plusieurs commandes | Datatype Channel point-à-point `commandes.recues`, puis le même `commandes.validees` que la saisie manuelle | Trois formats entrent, un seul sort : c'est la définition du Normalizer ; le style d'intégration reste le transfert de fichiers côté client, mais il débouche sur du messaging dès la porte d'entrée. |
| 2 | **Publish-Subscribe Channel** sur l'événement `CommandeExpediee` | Event Message, Recipient List si les préférences client (e-mail, SMS) décident des canaux ; Content Filter pour ne pas envoyer le prix au SMS | Publish-subscribe (topic) | Ajouter WhatsApp = ajouter un abonnement, sans toucher ni redéployer l'émetteur ; un ordre point-à-point obligerait le service d'expédition à connaître chaque canal. |
| 3 | **Splitter** | Message Translator (format WMS), Message Sequence (1/n … n/n), Datatype Channel par entrepôt | Point-à-point `entrepot.RBX`, `entrepot.LSQ` | Un message devient n messages, un par entrepôt ; chaque ordre est une commande pour un seul destinataire, donc point-à-point ; la séquence dit à l'aval combien d'ordres composent la commande. |
| 4 | **Aggregator** (défendable : **Process Manager** / saga) | Correlation Identifier (numéro de commande), Message Expiration ou délai de complétion pour l'alerte 48 h | Point-à-point `preparations.accuses` | n messages deviennent un : c'est l'Aggregator ; dès qu'il faut un état persisté, un délai et une compensation (annulation), c'est le Process Manager de la slide 18. Les deux réponses sont acceptées si le délai est traité. |
| 5 | **Content-Based Router** vers une file dédiée `preparations.urgentes` | Competing Consumers dédiés à la file urgente ; Resequencer défendable mais fragile (il retient les messages) | Deux canaux point-à-point, le second lu par des consommateurs réservés ou prioritaires | Service Bus n'a pas de priorité intra-file : la priorité s'obtient par une file séparée et des consommateurs qui la vident d'abord ; le routeur décide selon le contenu (`Urgente`). |
| 6 | **Wire Tap** + **Message Store** | Claim Check pour les PDF (blob + clé dans le message), Message History (`CausationId`) pour reconstituer le parcours | Publish-subscribe implicite : le wire tap copie vers `audit`, canal point-à-point vers le magasin | Copier sans perturber (Wire Tap) et conserver de façon consultable (Message Store) répondent aux deux contraintes « sans ralentir » et « sans modifier les consommateurs » ; le PDF de 2 Mo n'a rien à faire dans le message, d'où le Claim Check. |

## Points à faire ressortir en correction

- **Situation 1** : beaucoup répondent « Message Translator ». C'est juste mais incomplet : il y a trois formats,
  donc un routeur devant les traducteurs, ce que le livre nomme Normalizer. Demandez aussi « comment le fichier
  devient un message ? » pour faire nommer le Channel Adapter.
- **Situation 2** : si quelqu'un propose une Recipient List seule, faites remarquer que l'émetteur connaît alors
  la liste des destinataires : il faudra le modifier pour WhatsApp. Le publish-subscribe inverse la dépendance.
- **Situation 4** : la frontière Aggregator / Process Manager est une bonne discussion. Critère simple : si la
  seule règle est « quand j'ai tout, j'émets », c'est un Aggregator ; dès qu'il y a des décisions
  intermédiaires (relance, annulation, délai), c'est une saga.
- **Situation 5** : certains proposent un Resequencer ou une « file prioritaire ». Rappelez qu'un Resequencer
  retient les messages jusqu'à pouvoir les ordonner, ce qui est l'inverse de ce que veut l'ADV, et que les
  brokers cibles n'ont pas de priorité par message. Deux files + routeur est la réponse opérationnelle.
- **Situation 6** : le Claim Check est souvent oublié. Le message de 2 Mo multiplié par 900 commandes par jour,
  copié sur chaque abonnement, coûte cher et ralentit tout le monde.

## Bonus — pipeline complet

Canaux et sémantiques attendus sur le dessin :

```text
sftp (fichiers EDI) ─Channel Adapter─► commandes.recues (P2P) ─Normalizer─► commandes.validees (topic)
                                                                              ├─ Wire Tap ─► audit (P2P) ─► Message Store (+ Claim Check pour les PDF)
                                                                              ├─ Routeur urgent / normal ─► preparations.urgentes | preparations.normales (P2P)
                                                                              │        └─ Splitter + Translator ─► entrepot.RBX | entrepot.LSQ (P2P, 1/n)
                                                                              └─ (module 6) Outbox
preparations.accuses (P2P) ─Aggregator / Saga─► commandes.expediees (topic) ─► e-mail | SMS | extranet | WhatsApp (abonnements)
dead-letter : tout message expiré, rejeté ou en échec, avec canal et raison
```
