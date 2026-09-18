# Solution — Exercice 6.1 : Plan de migration Strangler de Trame

> Document formateur — Ne pas distribuer avant la restitution.

## Approche pédagogique

L'exercice n'a pas une réponse unique : il a des réponses **argumentées** et des réponses **fausses**. Est faux tout ordre qui commence par la facturation (le plus risqué, le plus dépendant) ou qui ignore la chaîne de dépendances sans prévoir l'adaptateur correspondant. Est argumenté tout ordre qui s'écarte du score en disant pourquoi (la douleur de Marc Vandewalle justifie d'avancer les ordres de préparation, à condition de financer un message bridge).

Faites restituer deux binômes aux ordres différents : la confrontation vaut mieux qu'un corrigé lu. Vous jouez Julien Delcourt : vous demandez « et si ça se passe mal, on fait quoi ? » à chaque bascule proposée. Un binôme qui n'a pas de critère de retour arrière mesurable n'a pas fini.

## Solution détaillée

### Partie 1 — La grille notée

| Réf. | Valeur | Risque | Dépendances | Score (2V − R − D) | Justification |
|---|---|---|---|---|---|
| F1 Catalogue et stock | 4 | 1 | 1 | **6** | 60 % des appels du SI, consommé par tous les clients à venir (extranet, scanners) ; lecture seule, donc aucune corruption possible ; ne dépend de rien, aucune file MSMQ |
| F2 Prise de commande | 5 | 4 | 2 | **4** | le cœur du métier, 900 commandes par jour ; risque élevé : 9 tables, 80 procédures, règle de remise dupliquée en T-SQL à reproduire au centime ; dépend peu (F5 lui envoie des commandes, F3 en découle) |
| F3 Ordres de préparation | 5 | 3 | 4 | **3** | la douleur immédiate (pertes MSMQ, Marc Vandewalle) ; risque moyen : deux files à éteindre, scanners MAUI à déployer ; forte dépendance : l'ordre est émis par la validation de F2 |
| F4 Facturation | 3 | 5 | 5 | **−4** | fonctionne, mensuel, mais 35 M€ et un contrôle de gestion intraitable ; risque maximal : COM+ / DTC, base comptable ; dépend de F2 et F3 (facture à l'expédition) |
| F5 EDI grands comptes | 3 | 3 | 3 | **0** | volume faible, clients stratégiques, pénalités contractuelles ; risque moyen : format figé, 20 variantes ; crée des commandes, donc dépend du modèle de F2 |

Écarts acceptables : ± 1 sur chaque note, tant que la justification cite un fait du tableau. Un binôme qui note F3 à valeur 5 et F1 à valeur 3 parce que « le catalogue marche déjà » a compris la valeur métier autrement ; l'ordre final change peu.

### Partie 2 — L'ordre de bascule daté

| Trimestre | Bascule | Justification | Prérequis | Critère de retour arrière |
|---|---|---|---|---|
| **T4 2026** | F1 Catalogue et stock | on commence par ce qui se lit : comparaison facile (même requête, même résultat), volume élevé qui prouve vite que Trame 2 tient la charge, aucun risque de corruption | vues SQL en lecture sur la base Trame (l'ACL de lecture) ; jeu de 500 requêtes de non-régression comparant Trame et Trame 2 | P95 supérieur à 300 ms sur 1 h, ou taux de 5xx supérieur à 1 % sur 24 h, ou écart de stock constaté entre les deux systèmes |
| **T1 2027** | F2 Prise de commande | la valeur maximale ; à ce stade, le noyau métier (module 1) et la persistance (module 4) sont en place ; F3 et F5 en dépendent, il faut donc passer par là avant | règle de remise **unique** dans `Domain`, testée au centime contre le T-SQL sur 1 000 commandes historiques (*shadow run* d'un mois : Trame 2 calcule, Trame reste maître) ; postes WinForms branchés sur la façade (voir bonus) | écart de montant sur l'échantillon quotidien de 50 commandes, ou 5xx supérieur à 0,5 % sur 24 h, ou temps de saisie ADV dégradé |
| **T2 2027** | F3 Ordres de préparation | découle directement de F2 (événement `CommandeValidee`) ; remplace les files MSMQ par le bus, avec Outbox ; les scanners MAUI arrivent | files MSMQ vidées et gelées à Lesquin puis Roubaix ; message bridge MSMQ → bus pendant un mois pour les commandes encore saisies dans Trame ; scanners déployés à Lesquin d'abord | tout ordre non émis dans les 5 minutes après validation (alerte Application Insights) ; Marc Vandewalle a le dernier mot |
| **T2 2027** | F5 EDI grands comptes | faible volume, mais crée des commandes : dès que F2 est stable, on arrête d'alimenter Trame par l'import nocturne | traducteur EDI → `Commande` (ACL) testé sur les 20 formats avec les fichiers réels du dernier trimestre ; double import en recette pendant deux semaines | un seul fichier rejeté par un grand compte, ou une pénalité contractuelle |
| **T4 2027** | F4 Facturation | en dernier : le plus risqué, le plus dépendant ; à ce stade, commandes et expéditions vivent déjà dans Trame 2 ; la base comptable, elle, ne bouge pas | adaptateur vers la base comptable (remplace COM+ / DTC par un message et une compensation) ; double run d'un mois de facturation, rapprochement par le contrôle de gestion | tout écart constaté par le contrôle de gestion sur le rapprochement mensuel ; archivage 10 ans vérifié |

Après F4, Trame n'a plus de route : extinction du serveur IIS, fin des 18 mois de coexistence (28/02/2028 au plus tard).

Variante défendable : avancer F3 en T1 2027, avant F2, parce que la perte d'ordres est le problème le plus douloureux. C'est acceptable **si** le binôme prévoit un message bridge MSMQ → bus dès T1 (Trame continue d'émettre les ordres via MSMQ, le bridge les republie vers le consumer de Trame 2) et accepte le coût d'un adaptateur supplémentaire. Sans le bridge, la variante est fausse : F3 sans F2, c'est un consumer sans producteur.

### Partie 3 — La façade YARP

| Route | Cluster | Bascule | Retour arrière | Anti-Corruption Layer |
|---|---|---|---|---|
| `/api/catalogue/{**rest}` | `trame2` | T4 2026 | `ClusterId` → `trame` | légère : vues SQL en lecture sur la base Trame ; aucune écriture |
| `/api/commandes/{**rest}` | `trame2` | T1 2027 | `ClusterId` → `trame` | oui : `TrameLegacyAdapter` écrit la commande dans le schéma de Trame tant que F3 et F4 y lisent ; façade CoreWCF pour les postes WinForms (bonus) |
| `/api/preparations/{**rest}` | `trame2` | T2 2027 | `ClusterId` → `trame` et réactivation du bridge | oui : message bridge MSMQ → bus pendant la transition |
| `/edi/{**rest}` | `trame2` | T2 2027 | `ClusterId` → `trame` (import nocturne réactivé) | oui : traducteur EDI → `Commande`, format figé par contrat |
| `/api/factures/{**rest}` | `trame2` | T4 2027 | `ClusterId` → `trame` | oui : adaptateur vers la base comptable, compensation à la place du DTC |
| `{**rest}` | `trame` | — | — | — |

Points à vérifier dans les copies :

- Les routes spécifiques précèdent la route `{**rest}` ; un binôme qui met la route `legacy` en premier a tout envoyé vers Trame.
- Le retour arrière est une ligne de configuration, pas un déploiement : c'est tout l'intérêt de YARP. Si le binôme écrit « redéployer Trame », il n'a pas compris le rôle du proxy.
- Au moins deux ACL identifiées : la base comptable et l'EDI sont les évidentes ; l'adaptateur d'écriture dans le schéma Trame pendant la coexistence est celle que les binômes oublient.

## Variantes acceptables

1. Formule de score différente (par exemple valeur − risque − dépendances, sans le facteur 2) : l'ordre reste F1, F2, F3, F5, F4 ou F1, F2, F5, F3, F4. Les deux sont bons.
2. F5 avant F3 en T2 2027 : indifférent, à condition que les deux soient après F2.
3. Un cinquième trimestre (T3 2027) pour séparer F3 et F5 : plus prudent, mais dépasse le calendrier imposé ; accepter si le binôme le dit et le justifie par la charge de l'équipe (six développeurs).
4. Un critère de retour arrière fondé sur un délai (« 48 h d'observation ») plutôt qu'un seuil : acceptable si un seuil chiffré l'accompagne.

## Erreurs classiques à repérer en correction

| Erreur observée | Cause probable | Comment corriger |
|---|---|---|
| F4 Facturation en premier « parce que le DTC tombe en panne » | confusion entre douleur technique et priorité de migration | rappeler que le DTC tombe lors des bascules réseau, rarement ; la facturation fonctionne et le contrôle de gestion refuse tout écart : dernier, avec double run |
| F3 avant F2 sans message bridge | chaîne de dépendances ignorée | demander « qui émet `CommandeValidee` tant que la commande est saisie dans Trame ? » ; la réponse est le bridge, et il a un coût |
| Aucun critère de retour arrière, ou « si ça ne marche pas » | notion de critère mesurable absente | imposer un chiffre, une durée, un observateur (P95, 5xx, écart de montant, ordre non émis en 5 min) |
| Table YARP avec la route `{**rest}` en tête | ordre d'évaluation des routes mal compris | rappeler que YARP évalue les routes les plus spécifiques d'abord ; la route fourre-tout est le filet, en dernier |
| Aucune ACL, « Trame 2 lira directement les tables de Trame » | Anti-Corruption Layer perçue comme du luxe | montrer ce que coûte une dépendance directe : chaque changement de schéma Trame casse Trame 2 ; l'adaptateur isole, et il disparaît avec Trame |
| Tout basculer en T4 2026 « puisque Trame 2 est prêt » | Strangler confondu avec big bang | 18 mois de coexistence sont dans le cahier des charges ; on bascule quand le retour arrière est possible, fonction par fonction |

## Points à insister en débriefing

- L'ordre importe moins que la **méthode** : critères explicites, dépendances écrites, retour arrière mesurable, prérequis nommés. C'est ce que Julien Delcourt présente à Nadia Benali, pas un calendrier optimiste.
- Chaque bascule est **réversible par configuration** : une route YARP, pas un déploiement. Tant que Trame tourne, on peut revenir ; c'est pour cela qu'on ne l'éteint qu'à la fin.
- L'Anti-Corruption Layer n'est pas de la sur-ingénierie : c'est le code qui **disparaît** à la fin de la migration. Tout ce que Trame 2 sait de Trame doit tenir dans les adaptateurs, nulle part ailleurs.
- Lien avec le TP 6 : la bascule F3 est exactement ce que le TP construit — Outbox, relais, consumer idempotent — et le pipeline est ce qui permet de basculer une route en confiance.

## Bonus — les postes WinForms pendant 18 mois

Réponse attendue, en cinq lignes :

Conserver les services WCF de Trame pour F1 (lecture) ne coûte rien puisque Trame reste en place. Pour F2, une fois la prise de commande basculée, les postes WinForms doivent écrire dans Trame 2 : exposer l'ancien contrat `ICommandeService` depuis Trame 2 avec **CoreWCF**, implémenté par un adaptateur qui appelle l'API REST, coûte deux semaines et ne touche pas aux postes ; c'est l'option de transition retenue. Le replatform des postes sur .NET 10 avec un client REST généré par Kiota est la cible, planifiée sur T2 et T3 2027, poste par poste, parce qu'il libère des serveurs WCF et retire CoreWCF. Risque principal de l'option CoreWCF : oublier de l'éteindre ; on met la date de retrait dans l'ADR.
