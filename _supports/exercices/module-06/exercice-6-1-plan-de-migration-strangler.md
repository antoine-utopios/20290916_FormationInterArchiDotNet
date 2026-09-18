# Exercice 6.1 — Plan de migration Strangler de Trame

> Module : 6 — Legacy, messaging, identité d'entreprise et industrialisation
> Durée estimée : 20 min
> Difficulté : 2 / 5
> Type : exercice d'analyse en binôme, sans code

## Objectifs pédagogiques

À la fin de cet exercice, vous serez capable de :

- Ordonner une migration Strangler Fig avec des critères explicites (valeur métier, risque technique, dépendances) plutôt qu'à l'intuition
- Définir la façade YARP qui accompagne chaque bascule : route, cluster cible, date, critère de retour arrière
- Repérer les endroits où une Anti-Corruption Layer est indispensable pour que Trame 2 ne dépende jamais des formats de Trame

## Prérequis

- Avoir suivi la section 1 du module 6 (stratégies de modernisation, Strangler Fig, façade YARP)
- Avoir sous les yeux le tableau du SI « Trame » et la cible « Trame 2 » de `FIL-ROUGE.md`
- Aucun poste nécessaire : une feuille, ou un fichier Markdown partagé dans le binôme

## Contexte

Nadia Benali (DSI) a validé le programme Trame 2 à une condition : Trame reste en production jusqu'au bout, et chaque bascule doit pouvoir être annulée en moins d'une heure. Julien Delcourt (architecte) doit lui présenter l'ordre de bascule des cinq grandes fonctionnalités, trimestre par trimestre, de T4 2026 à T4 2027. Les postes WinForms de l'ADV restent en service 18 mois : ils appellent encore les services WCF de Trame.

Le reverse proxy YARP est déjà en place devant IIS : aujourd'hui, toutes les routes pointent vers Trame. Chaque fonctionnalité migrée se traduit par une route de plus vers Trame 2.

Les cinq fonctionnalités à ordonner, avec les faits utiles pour noter :

| Réf. | Fonctionnalité | Comment Trame la réalise aujourd'hui | Faits utiles |
|---|---|---|---|
| F1 | Catalogue et consultation de stock | service WCF `CatalogueService.svc`, 40 000 références, 12 procédures stockées de lecture | lecture seule ; 60 % des appels du SI ; P95 attendu < 300 ms ; aucune file MSMQ ; consommé par l'extranet, les postes ADV et bientôt les scanners |
| F2 | Prise de commande | écran WinForms ADV + WCF `CommandeService.svc` ; 900 commandes par jour en pointe ; règles de tarif dupliquées entre C# et triggers T-SQL | écrit dans 9 tables ; 80 procédures stockées ; déclenche F3 par MSMQ ; la remise (0 à 25 % + 5 % volume, plafond 30 %) doit rester identique au centime |
| F3 | Ordres de préparation | files MSMQ locales vers Roubaix et Lesquin ; pertes de messages au reboot ; aucune supervision | dépend de F2 (émis à la validation) ; Marc Vandewalle attend l'application scanner MAUI ; 2 files MSMQ à éteindre |
| F4 | Facturation | composants COM+ et transaction DTC entre la base commandes et la base comptable ; run mensuel ; 35 M€ de chiffre d'affaires | dépend de F2 et F3 (facture à l'expédition) ; pannes DTC lors des bascules réseau ; archivage 10 ans ; le contrôle de gestion refuse tout écart |
| F5 | EDI grands comptes | fichiers déposés sur un partage réseau, import nocturne par procédure stockée ; 20 grands comptes | crée des commandes (donc dépend de F2) ; format de fichier figé par contrat, pénalités en cas de rejet ; volume faible mais clients stratégiques |

## Énoncé

### Partie 1 — Noter chaque fonctionnalité (8 min)

Remplissez la grille suivante. Chaque note va de 1 à 5 et doit être justifiée en une ligne, à partir des faits du tableau ci-dessus, pas d'une impression.

| Réf. | Valeur métier (5 = très forte) | Risque technique (5 = très risqué) | Dépendances (5 = très dépendante) | Justification |
|---|---|---|---|---|
| F1 | | | | |
| F2 | | | | |
| F3 | | | | |
| F4 | | | | |
| F5 | | | | |

Pour classer, calculez un score de priorité : **score = 2 × valeur − risque − dépendances**. Le score n'est qu'un point de départ : si votre ordre final s'en écarte, dites pourquoi.

Résultat attendu : cinq lignes notées et justifiées, un score par ligne.

### Partie 2 — Ordonner et dater les bascules (7 min)

Répartissez les cinq fonctionnalités sur les quatre trimestres T4 2026, T1 2027, T2 2027, T4 2027 (un trimestre en accueillera deux). Pour chaque bascule, écrivez :

1. La justification en deux lignes (pourquoi maintenant, pourquoi pas avant).
2. Le **critère de retour arrière** mesurable qui déclenche le rebranchement sur Trame : par exemple « taux d'erreurs 5xx supérieur à 1 % sur 24 h », « P95 supérieur à 300 ms », « écart de facturation constaté par le contrôle de gestion ».
3. Ce qui doit être **vrai avant** de basculer (prérequis) : données migrées, doublon de règle supprimé, file MSMQ vidée, etc.

Résultat attendu : une frise en quatre trimestres, chaque bascule avec sa justification, son critère de retour arrière et ses prérequis.

### Partie 3 — La façade YARP (5 min)

Complétez la table de routes que Sofia Marques mettra dans la configuration du proxy. L'ordre des lignes compte : YARP évalue les routes les plus spécifiques d'abord, et la route « tout le reste » doit rester en dernier vers Trame.

| Route (motif de chemin) | Cluster | Trimestre de bascule | Retour arrière | Anti-Corruption Layer nécessaire ? |
|---|---|---|---|---|
| `/api/catalogue/{**rest}` | | | | |
| | | | | |
| | | | | |
| | | | | |
| | | | | |
| `{**rest}` | `trame` | — | — | — |

Pour la colonne ACL, indiquez pour chaque bascule si Trame 2 doit parler à un morceau de Trame qui reste en place (base comptable, format EDI, service WCF), et donc s'il faut un adaptateur qui traduit vers les contrats de Trame 2.

Résultat attendu : une table de routes complète et ordonnée, avec au moins deux adaptateurs identifiés.

## Indices (à consulter si bloqué)

<details>
<summary>Indice 1 — Par quoi commencer</summary>

Commencez par ce qui se lit et ne s'écrit pas : une fonctionnalité en lecture seule ne peut pas corrompre de données, se compare facilement à l'ancienne (même requête, mêmes résultats) et se rebranche sans conséquence. Le volume d'appels élevé est un atout, pas un risque : il prouve vite que Trame 2 tient la charge.

</details>

<details>
<summary>Indice 2 — Les dépendances dessinent une chaîne</summary>

F5 crée des commandes (F2), F2 déclenche F3, F4 attend F2 et F3. Migrer une fonctionnalité avant celles dont elle dépend impose de faire cohabiter deux modèles de données ; c'est possible, mais cela coûte un adaptateur de plus. Suivez la chaîne, sauf si la valeur métier justifie de la rompre.

</details>

<details>
<summary>Indice 3 — Les routes YARP</summary>

Une route par préfixe d'URL : `/api/catalogue/{**rest}`, `/api/commandes/{**rest}`, `/api/preparations/{**rest}`, `/api/factures/{**rest}`, `/edi/{**rest}`. Le motif `{**rest}` capture tout ce qui suit. La route finale `{**rest}` sans préfixe est le filet de sécurité vers Trame. Le retour arrière d'une route = changer son `ClusterId` de `trame2` à `trame`.

</details>

<details>
<summary>Indice 4 — Où va l'Anti-Corruption Layer</summary>

Posez la question par bascule : « après cette bascule, Trame 2 doit-il envoyer ou recevoir quelque chose dans un format qui appartient à Trame ? » Si oui, l'adaptateur vit côté Trame 2, dans `Infrastructure`, et convertit vers les entités du `Domain`. La base comptable et les fichiers EDI sont les deux candidats évidents.

</details>

## Pour aller plus loin (bonus)

Les postes WinForms appellent encore les contrats WCF (SOAP, `basicHttpBinding`) pendant 18 mois. YARP peut leur router du HTTP, mais Trame 2 n'expose que du REST. Proposez, en cinq lignes, la solution que vous défendriez devant Julien Delcourt parmi : conserver les services WCF de Trame jusqu'au retrait des postes ; exposer les anciens contrats depuis Trame 2 avec CoreWCF, comme façade de transition ; replatformer d'abord les postes WinForms sur .NET 10 et les faire passer au client REST généré (Kiota). Donnez le coût et le risque de l'option retenue.
