# Solution — Exercice 1.1 : Choisir un style d'architecture pour cinq besoins Textinord

> Document formateur — Ne pas distribuer avant la fin de l'exercice.

## Approche pédagogique

Il n'y a pas une réponse unique par besoin : on évalue la **démarche** —
un style nommé, deux critères de la grille, une contrainte Textinord
chiffrée — et la capacité à résister au réflexe « micro-services ». Pendant
le travail en binôme, circulez et repérez les justifications sans critère
(« c'est plus moderne », « c'est ce qu'on fait chez mon client ») : demandez
« quel critère ? quelle contrainte ? » plutôt que de corriger le choix.

En restitution, jouez Julien Delcourt : vous acceptez une réponse différente
de celle ci-dessous si elle est argumentée avec la grille ; vous refusez une
réponse identique qui ne l'est pas.

## Solution détaillée

### Besoin A — L'extranet clients

**Style retenu** : n-tiers (Blazor Web App → API REST ASP.NET Core →
données), l'API étant celle de Trame 2 (monolithe modulaire), le tout
stateless derrière Azure Container Apps.

Justification attendue :

- **Évolutivité** : le pic de septembre se traite par scale-out horizontal
  d'instances stateless (l'état du panier vit côté client ou dans Redis),
  sans redéployer quoi que ce soit ; 900 commandes par jour restent faibles
  pour une instance, mais la consultation du catalogue (40 000 références,
  15 000 lectures par jour) justifie un cache Redis dès la v1.
- **Sécurité** : Entra External ID en OIDC, l'API ne voit que des jetons ;
  aucune identité client ne transite par Trame.
- **Réversibilité / compatibilité** : l'extranet consomme l'API versionnée
  `/api/v1` ; tant que Trame vit, l'API va chercher certaines données dans
  SQL Server via l'ACL, sans que l'extranet le sache.

Variante acceptable : SPA Angular + API (même raisonnement, choix front
différent, voir module 3).

### Besoin B — L'application des préparateurs d'entrepôt

**Style retenu** : client .NET MAUI (Android) « offline-first » + API REST +
**event-driven** pour la distribution des ordres (Service Bus, topic
`preparations`) ; verrou pessimiste explicite sur l'ordre pris.

Justification attendue :

- **Fiabilité** : le Wi-Fi qui décroche est la normale, pas l'exception ;
  l'application stocke localement l'ordre en cours et synchronise au retour
  du réseau ; l'ordre arrive par message persistant (plus jamais de perte
  MSMQ au reboot).
- **Performance** : 15 000 mouvements de stock par jour, soit environ 1 par
  seconde en pointe — aucune difficulté pour une API, à condition que la
  saisie ne bloque pas sur le réseau (asynchronisme).
- **Concurrence** : « un ordre pris ne doit pas être repris » est un
  Pessimistic Offline Lock avec expiration, porté par l'API, pas par le
  scanner.

Piège fréquent : proposer SignalR « temps réel » comme seul canal. SignalR
convient pour notifier un écran connecté, pas pour garantir la livraison
d'un ordre à un scanner qui était hors ligne.

### Besoin C — L'EDI avec les 20 grands comptes

**Style retenu** : intégration par **transfert de fichiers** + pipeline de
messages (style event-driven interne) avec un **Adapter par grand compte**
(agent métier) et un **Anti-Corruption Layer** vers le modèle de commande
Trame 2. Chargement des agents en plug-in (`AssemblyLoadContext`) ou, plus
simplement, une classe par format enregistrée dans la DI.

Justification attendue :

- **Maintenabilité** : vingt formats qui évoluent indépendamment ; une
  classe par format (OCP) évite qu'un changement chez un grand compte ne
  redéploie tout le reste.
- **Fiabilité** : une commande EDI mal intégrée coûte une pénalité ; chaque
  fichier passe par un canal avec dead letter et reprise, jamais par un
  traitement synchrone qui échoue en silence.
- **Coût / réversibilité** : SFTP + fichiers reste le contrat imposé par
  les clients ; on ne leur demandera pas une API. L'ESB « SOA » complet
  serait un point central lourd pour 20 flux quotidiens.

Variante acceptable : SOA / ESB si le binôme justifie par la gouvernance
des contrats — à condition d'assumer le coût d'exploitation.

### Besoin D — Le reporting de la direction

**Style retenu** : **CQRS** au niveau des données — un modèle de lecture
séparé (base analytique ou vues matérialisées, alimentées la nuit ou par
événements), consommé par un tableau de bord (Power BI, ou une page Blazor
en lecture seule). Pas de micro-service, pas d'appel direct à la base
transactionnelle.

Justification attendue :

- **Performance** : des requêtes lourdes sur 10 ans de commandes (environ
  2 millions de commandes, 7 millions de lignes) ne doivent pas concurrencer
  la validation des commandes du matin ; la séparation lecture / écriture
  protège le P95 de l'API (< 300 ms sur le stock).
- **Coût** : aucune écriture, une consultation quotidienne — une projection
  nocturne suffit, pas de cluster temps réel.
- **Observabilité / archivage** : l'archivage légal de 10 ans devient une
  propriété du modèle de lecture, pas une contrainte de la base
  transactionnelle.

### Besoin E — La notification des clients

**Style retenu** : **event-driven** — la commande publie un événement
(`CommandeValidee`, `CommandeExpediee`) sur un topic Service Bus ; un
consommateur dédié (worker) envoie l'email via un fournisseur ; pattern
Outbox pour ne jamais perdre l'événement.

Justification attendue :

- **Fiabilité** : « ne doit jamais faire échouer la validation » impose de
  découpler dans le temps ; si le fournisseur d'emails est en panne, le
  message attend dans la file, avec retry puis dead letter.
- **Performance** : la validation n'attend pas un appel HTTP sortant ; l'API
  répond dans son budget de 300 ms.
- **Maintenabilité** : ajouter un canal (SMS, notification mobile) = un
  consommateur de plus, sans toucher à la validation (OCP à l'échelle du
  système).

### Partie 2 — Le piège

Le besoin visé est **A (l'extranet)** ou, selon les groupes, **B** : c'est
là que l'on entend « on fait un micro-service catalogue, un micro-service
panier, un micro-service commande ». Réponse attendue : avec 6 développeurs
et 900 commandes par jour, la charge ne justifie pas une exploitation
répartie (réseau, données réparties, observabilité distribuée, compétences)
; le monolithe modulaire donne les mêmes frontières de modules, dans un seul
déploiement. Cela deviendrait pertinent si une équipe distincte prenait en
charge un module avec son propre rythme de livraison, ou si un module avait
un profil de charge très différent des autres (par exemple le catalogue
public exposé à des dizaines de milliers de visiteurs anonymes).

Certains binômes citent **E** comme piège : acceptable si l'argument est
« un worker de notification n'est pas un micro-service, c'est un consommateur
du même monolithe déployé séparément ». La nuance mérite d'être dite à
voix haute.

## Variantes acceptables

1. Extranet en SPA Angular + API plutôt qu'en Blazor : même architecture,
   choix de front débattu au module 3.
2. Reporting via Azure Synapse / Fabric ou un simple SQL avec vues
   indexées : la réponse porte sur la séparation lecture / écriture, pas
   sur l'outil.
3. EDI traité en batch nocturne unique plutôt qu'en pipeline de messages :
   acceptable si le binôme explique comment il gère un fichier en erreur
   sans bloquer les dix-neuf autres.

## Erreurs classiques à repérer en correction

| Erreur observée | Cause probable | Comment corriger |
|---|---|---|
| « Micro-services » pour l'extranet sans critère | réflexe de mode | Faire compter les développeurs (6) et les services (3+) : qui exploite quoi la nuit ? |
| Notification envoyée en synchrone dans l'API « parce que c'est plus simple » | asynchronisme pas encore intégré | Relire la phrase « ne doit jamais faire échouer » ; montrer la file et la dead letter |
| Reporting branché sur la base transactionnelle | CQRS perçu comme complexe | Expliquer que CQRS commence par une vue dédiée, pas par de l'event sourcing |
| EDI traité par un « ESB » sans dire qui l'exploite | SOA cité de mémoire | Demander le coût et la réversibilité ; comparer avec un pipeline de messages interne |
| Justification sans contrainte chiffrée | grille non utilisée | Renvoyer à la slide « Les critères d'une bonne architecture » et au fil rouge |

## Points à insister en débriefing

- Un style se choisit avec des critères classés ; le premier critère de
  Textinord est la **fiabilité** (pertes MSMQ, DTC, régressions), le second
  le **coût d'exploitation** (pas d'équipe dédiée).
- Les styles se **combinent** : n-tiers pour l'extranet, event-driven pour
  les notifications et les ordres, CQRS pour le reporting — dans un seul
  monolithe modulaire déployé en plusieurs processus (API, worker).
- Lien avec le module 2 : les besoins B, C et E sont exactement les
  scénarios des patterns d'intégration (canaux, routage, dead letter) vus
  cet après-midi.

## Bonus

Scénario attendu pour le besoin B : l'ordre est marqué « pris » localement
avec un horodatage ; les mouvements saisis hors ligne sont journalisés dans
une file locale (SQLite) ; au retour du réseau, l'application rejoue la file
vers l'API ; l'API vérifie le verrou : si un autre préparateur a pris l'ordre
pendant la coupure (expiration du verrou dépassée), elle refuse les
mouvements avec un code d'erreur explicite et l'application affiche le
conflit — c'est un Optimistic Offline Lock sur les mouvements, combiné au
verrou pessimiste sur l'ordre.
