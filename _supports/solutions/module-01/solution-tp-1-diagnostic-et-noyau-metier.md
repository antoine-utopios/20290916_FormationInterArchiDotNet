# Solution — TP 1 : Diagnostic de Trame et noyau métier de Trame 2

> Document formateur — Ne pas distribuer avant la fin du TP.
> Code complet et compilable : `solutions/module-01/tp-1/` (`dotnet build` puis `dotnet test` : 29 tests verts sur .NET 10).

## Approche pédagogique

Le TP a deux moitiés de nature différente. La partie A (diagnostic, ADR)
n'a pas de réponse unique : on évalue la structure des ADR et la présence
de critères et de contraintes. La partie B a un critère dur, vérifiable en
trente secondes : le projet `Domain` n'a **aucun `PackageReference`** et
`dotnet test` est vert. Passez dans les binômes à la fin de B3 (25 minutes
environ) : c'est là que se décide si le temps suffira pour la machine à
états. Un binôme qui n'a pas fini B5 mais dont B2 à B4 sont propres et
testés a réussi le TP.

## Partie A — Diagnostic et ADR

### A1 — Cartographie attendue (`docs/diagnostic-trame.md`)

| Brique | Technologie actuelle | Problème constaté | Qualité la plus touchée | Cible Trame 2 |
|---|---|---|---|---|
| Gestion des commandes (ADV, achats, entrepôt) | WinForms .NET Framework 4.8, code-behind | 180 000 lignes, aucune couche métier, tests impossibles | Maintenabilité | Noyau `Domain` + API ASP.NET Core 10 ; Blazor et MAUI en clients |
| Services internes | WCF SOAP dans IIS | contrats figés, pas de versioning, clients non-.NET exclus | Réversibilité | Minimal APIs REST + OpenAPI versionnées (gRPC en interne si besoin) |
| Ordres de préparation | MSMQ, files locales | Windows seulement, pertes au reboot, aucune supervision | Fiabilité | Azure Service Bus (RabbitMQ en dev) via MassTransit, Outbox |
| Base de données | SQL Server 2016, 400 procédures, triggers métier | règles de tarif dupliquées C# / T-SQL | Maintenabilité | Azure SQL + EF Core 10 ; règles dans `Domain` ; ACL pendant la migration |
| Site B2C | ASP.NET Web Forms | non migrable | Coût | Blazor Web App (extranet), refonte |
| Annuaire et droits | Active Directory, groupes AD | pas d'accès partenaires, pas de MFA | Sécurité | Entra ID (hybride Entra Connect) + Entra External ID, OIDC |
| Transactions inter-bases | COM+ / DTC | pannes DTC lors des bascules réseau | Fiabilité | `TransactionScope` local + Outbox + sagas |
| Livraison | copie manuelle, aucune automatisation | mise en production trimestrielle, régressions | Observabilité / maintenabilité | Azure DevOps Pipelines, tests automatisés, releases mensuelles puis continues |

### A2 — Trois ADR modèles

`docs/adr/ADR-001-style-architecture.md` :

```text
# ADR-001 — Trame 2 est un monolithe modulaire en couches, déployé en plusieurs processus
Statut : accepté
Date : 2026-09-07
Décideurs : Julien Delcourt (architecte), Nadia Benali (DSI), Sofia Marques (lead dev)

## Contexte
Six développeurs .NET, 900 commandes par jour en pointe, 40 000 références,
deux entrepôts. Aucune équipe d'exploitation dédiée ; hébergement PaaS Azure.
Trame (WinForms + WCF + MSMQ) doit être remplacé sans arrêt de production.

## Options considérées
1. Micro-services (un service par domaine, une base par service) —
   autonomie de déploiement ; mais exploitation répartie, données réparties,
   compétences absentes, coût Azure supérieur.
2. Monolithe modulaire (modules Commandes, Catalogue, Préparation, Tarification
   dans une solution, déployé en API + worker) — un déploiement, frontières
   de modules imposées par les références de projets ; scalabilité horizontale
   du même binaire.
3. n-tiers classique sans modules — le plus rapide ; mais reproduit le couplage
   de Trame à moyen terme.

## Décision
Option 2. Critères décisifs : coût d'exploitation (pas d'équipe pour opérer
dix services) et maintenabilité (les frontières de modules donnent les mêmes
bénéfices de découpage sans le réseau).

## Conséquences
+ Un pipeline, un déploiement, des tests d'intégration simples.
+ Les modules peuvent devenir des services plus tard si un besoin d'échelle
  apparaît (catalogue public).
- Discipline nécessaire sur les références entre modules (analyzers,
  validation d'architecture au module 2).
- Un incident dans un module peut affecter les autres : à compenser par
  l'asynchronisme (worker séparé) et les health checks.
```

`docs/adr/ADR-002-decoupage-en-couches.md` :

```text
# ADR-002 — Clean architecture : Domain, Application, Infrastructure, points d'entrée
Statut : accepté
Date : 2026-09-07
Décideurs : Julien Delcourt, Sofia Marques

## Contexte
Les règles de Trame vivent dans des écrans et des triggers ; elles divergent.
Trame 2 doit avoir une seule source de vérité pour les règles, testable sans
base de données.

## Options considérées
1. n-tiers « classique » (UI → BLL → DAL) — la BLL dépend de la DAL ; les
   règles restent couplées à SQL.
2. Clean architecture (Domain au centre, Application, Infrastructure,
   Api/Extranet/Entrepot/Worker à la périphérie) — la règle de dépendance
   pointe vers le domaine ; l'infrastructure implémente les ports.
3. Vertical slices seules — bonne organisation par fonctionnalité, mais sans
   noyau partagé les règles de remise seraient dupliquées entre tranches.

## Décision
Option 2, avec des vertical slices à l'intérieur de la couche Application.
Critères : maintenabilité (règles testées seules) et réversibilité (changer
de base ou de bus ne touche pas Domain ni Application).

## Conséquences
+ `Textinord.Trame.Domain` : aucun PackageReference, jamais.
+ `Application` ne référence que `Domain` ; `Infrastructure` référence
  `Application` et implémente ses ports ; `Api`, `Extranet`, `Entrepot`,
  `Worker` référencent `Application` et `Infrastructure` pour la composition.
- Plus de projets et de types (DTO, ports) : coût accepté ; les analyzers
  et un test d'architecture vérifieront les références (module 2).
- Interdit : `using Microsoft.EntityFrameworkCore` hors d'Infrastructure.
```

`docs/adr/ADR-003-strategie-de-migration.md` :

```text
# ADR-003 — Migration progressive de Trame par Strangler Fig derrière YARP
Statut : accepté
Date : 2026-09-07
Décideurs : Julien Delcourt, Nadia Benali, Awa Diop (cheffe de projet)

## Contexte
Trame reste en production ; 18 mois de compatibilité avec les postes
WinForms restants sont imposés ; les 20 grands comptes EDI ne changeront pas
de format ; l'équipe ne peut pas geler les évolutions pendant deux ans.

## Options considérées
1. Big bang (réécriture puis bascule) — simple à raconter ; deux ans sans
   valeur livrée, risque de bascule maximal.
2. Strangler Fig : reverse proxy (YARP) devant Trame et Trame 2, bascule
   fonctionnalité par fonctionnalité, ACL devant la base partagée.
3. Coexistence longue sans plan de retrait — c'est l'option 2 sans fin de
   Trame : coût double indéfini.

## Décision
Option 2, en commençant par les commandes (T1 2027), puis la préparation
(T3 2027), puis tarifs et achats (T1 2028). Critères : fiabilité (chaque
bascule est réversible en changeant une route) et coût (valeur livrée dès
le premier trimestre).

## Conséquences
+ Chaque fonctionnalité migrée a son ADR et sa route YARP ; retour arrière
  en minutes.
+ Le contrat d'API est versionné dès la v1 (/api/v1) et maintenu 18 mois.
- Double exploitation pendant 18 mois ; l'ACL doit traduire les procédures
  stockées et les codes de Trame vers le modèle de Trame 2.
- Un même client peut voir l'ancienne et la nouvelle IHM pendant la
  transition : communication à prévoir (Awa Diop).
```

## Partie B — Le noyau métier : choix et explications

Le code complet est dans `solutions/module-01/tp-1/`. Arborescence :

```
tp-1/
  Textinord.Trame.sln
  Directory.Build.props                 (LangVersion 14, nullable, analyzers, NoWarn CA1000 documenté)
  .gitignore
  src/Textinord.Trame.Domain/
    Communs/Erreur.cs, Result.cs, Specification.cs
    Catalogue/Article.cs, Entrepot.cs
    Clients/Client.cs, ConditionTarifaire.cs
    Tarification/Remise.cs, CalculRemise.cs
    Commandes/StatutCommande.cs, NumeroCommande.cs, IGenerateurNumeroCommande.cs,
              GenerateurNumeroCommandeEnMemoire.cs, LigneCommande.cs,
              OrdrePreparation.cs, Commande.cs
    Commandes/Specifications/StockDisponibleSpecification.cs,
                             CommandeValidableSpecification.cs
  tests/Textinord.Trame.Domain.Tests/
    Fixtures/JeuDeDonnees.cs
    Tarification/CalculRemiseTests.cs        (5 tests dont une Theory à 3 cas)
    Commandes/NumeroCommandeTests.cs         (3 tests dont une Theory à 5 cas)
    Commandes/CommandeTests.cs               (9 tests dont une Theory à 4 cas)
    Communs/SpecificationEtResultTests.cs    (3 tests)
```

`dotnet test` : 29 cas exécutés, 0 échec.

### B2 — `Result` et `Specification`

`Result` est une classe de base non générique (`Ok()`, `Echec(code, message)`)
et `Result<T>` en hérite. Lire `Valeur` sur un échec lève
`InvalidOperationException` : c'est délibéré, un code qui lit la valeur sans
tester `EstSucces` est un bug, pas un cas métier. `Selon(siSucces, siEchec)`
évite ce piège en forçant le traitement des deux branches.

`Specification<T>` est abstraite ; les combinateurs `Et`, `Ou`, `Non`
retournent des classes internes. `Specification<T>.Depuis(predicat)` sert
aux tests. Point d'attention : l'analyzer CA1000 refuse les membres
statiques sur les types génériques ; on le désactive dans
`Directory.Build.props` avec un commentaire, car `Result<T>.Ok(...)` est
l'usage idiomatique du pattern.

### B3 — Le modèle

- `ConditionTarifaire` est un `record` (égalité par valeur) validé à la
  construction : `new ConditionTarifaire("XXL", 0.26m)` lève
  `ArgumentOutOfRangeException`. Les valeurs prédéfinies sont des champs
  `static readonly`.
- `Entrepot` porte une `Priorite` : c'est la règle « Roubaix sert avant
  Lesquin » rendue explicite, plutôt qu'un ordre alphabétique accidentel.
- `Article.AvecStock(entrepot, quantite)` retourne `this` (style Builder,
  utile dans les tests) ; `EntrepotPouvantServir(quantite)` filtre, trie
  par priorité, prend le premier.

### B4 — La remise

```csharp
public static Remise Calculer(decimal tauxClient, int quantite)
{
    ArgumentOutOfRangeException.ThrowIfNegative(tauxClient);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantite);

    var tauxVolume = quantite > SeuilVolume ? TauxVolume : 0m;
    var tauxApplique = Math.Min(tauxClient + tauxVolume, Plafond);

    return new Remise(tauxClient, tauxVolume, tauxApplique);
}
```

Deux choix à expliquer : le seuil est strict (`> 500`, la 501e pièce
déclenche) — c'est la lecture de « au-delà de 500 pièces » ; et la fonction
accepte un `decimal` brut en plus de la surcharge `ConditionTarifaire`, ce
qui permet de tester l'écrêtage à 30 % avec un taux de 28 % que la condition
tarifaire, plafonnée à 25 %, ne permettrait pas de construire. Le record
`Remise` conserve les composantes (`TauxClient`, `TauxVolume`) : on peut
expliquer la remise au client, pas seulement l'appliquer.

### B5 — L'agrégat `Commande`

La table des transitions est un `FrozenDictionary` statique — écrite une
fois, lisible comme une spécification :

```csharp
private static readonly FrozenDictionary<StatutCommande, StatutCommande[]> Transitions =
    new Dictionary<StatutCommande, StatutCommande[]>
    {
        [StatutCommande.Brouillon] = [StatutCommande.Validee, StatutCommande.EnAttenteStock, StatutCommande.Annulee],
        [StatutCommande.EnAttenteStock] = [StatutCommande.Validee, StatutCommande.Annulee],
        [StatutCommande.Validee] = [StatutCommande.EnPreparation, StatutCommande.Annulee],
        [StatutCommande.EnPreparation] = [StatutCommande.Expediee],
        [StatutCommande.Expediee] = [StatutCommande.Facturee],
        [StatutCommande.Facturee] = [],
        [StatutCommande.Annulee] = [],
    }.ToFrozenDictionary();
```

`Transiter(cible)` consulte la table et retourne un `Result` en échec
(`TRANSITION_INTERDITE`) plutôt qu'une exception : l'annulation d'une
commande en préparation est un refus métier prévisible, que l'API renverra
en 409 au module 5.

`Valider()` :

```csharp
public Result<StatutCommande> Valider()
{
    if (_lignes.Count == 0)
    {
        return Result<StatutCommande>.Echec("COMMANDE_VIDE", "Une commande sans ligne ne se valide pas.");
    }

    var regle = new CommandeValidableSpecification();
    if (!regle.EstSatisfaitePar(this))
    {
        var transition = Transiter(StatutCommande.EnAttenteStock);
        return transition.EstSucces
            ? Result<StatutCommande>.Ok(Statut)
            : Result<StatutCommande>.Echec(transition.Erreur!);
    }

    var passage = Transiter(StatutCommande.Validee);
    if (passage.EstEchec)
    {
        return Result<StatutCommande>.Echec(passage.Erreur!);
    }

    EmettreOrdresPreparation();
    return Result<StatutCommande>.Ok(Statut);
}
```

Deux décisions à commenter avec les binômes :

- Le passage en `EnAttenteStock` est un **succès** (`Ok(EnAttenteStock)`),
  pas un échec : la commande a bien été traitée, son statut dit le reste.
  Un échec est réservé aux cas où rien ne s'est passé (commande vide,
  transition interdite).
- `EmettreOrdresPreparation()` affecte chaque ligne au premier entrepôt
  capable (par priorité), puis groupe par entrepôt : une commande dont une
  ligne est servie par Roubaix et une autre par Lesquin produit deux
  ordres, dans l'ordre des priorités. Le test
  `La_validation_emet_un_ordre_de_preparation_par_entrepot_concerne` vérifie
  exactement ce cas avec 500 vestes (Roubaix n'en a que 300 : Lesquin sert)
  et 1 000 gants (Roubaix seul).

`Expedier()` refuse tant qu'un ordre n'est pas `Clos` (code
`ORDRES_OUVERTS`), ce qui matérialise la règle « l'expédition est déclarée
quand tous les ordres sont clos ».

### Ce que les tests racontent

Les fixtures (`JeuDeDonnees`) portent des noms Textinord : Mairie de
Roubaix (collectivité, 10 %), Hôtel du Beffroi (hôtellerie, 12 %), Aciéries
de Denain (grand compte, 25 %), Atelier Dupont (standard). Les articles :
veste de travail VT-4410 (300 à Roubaix, 800 à Lesquin), gant EPI-2205
(2 000 à Roubaix seulement), drap LH-0901 en rupture partout. Un test se
lit alors comme un scénario : « l'Hôtel du Beffroi commande 50 draps en
rupture : la commande passe en attente de stock, aucun ordre n'est émis,
elle reste modifiable ».

## Variantes acceptables

1. Machine à états par `switch` expression au lieu d'un dictionnaire :
   acceptable si les transitions sont écrites à un seul endroit.
2. `Valider()` qui retourne `Result` (sans valeur) et laisse lire `Statut` :
   acceptable ; la version typée rend le test plus lisible.
3. Allocation des entrepôts déléguée à un service de domaine
   (`AffectationEntrepots`) plutôt qu'à l'agrégat : acceptable, et même
   préférable si la règle devient plus riche (distance, coût de transport).
4. Événements de domaine (`CommandeValidee`) collectés dans l'agrégat : bonus,
   utile au module 2 ; ne pas pénaliser leur absence.

## Erreurs classiques à repérer en correction

| Erreur observée | Cause probable | Comment corriger |
|---|---|---|
| `PackageReference` à EF Core dans `Domain` « pour les annotations » | réflexe Data Annotations | Les invariants se valident dans les constructeurs ; le mapping se fait en fluent dans Infrastructure (module 4) |
| Setters publics partout, règles dans les tests | anémie du modèle | Faire passer une règle (quantité positive) dans `AjouterLigne` et montrer que le test devient plus court |
| Transitions vérifiées par des `if` dispersés dans chaque méthode | pas de table centrale | Réunir dans un dictionnaire ; compter les lignes économisées |
| Exception pour une transition interdite | Result pas encore intégré | Demander ce que l'API doit renvoyer : un 409 avec un code, pas une stack trace |
| Seuil de volume à `>= 500` | lecture de la règle | « au-delà de 500 » ; le test à 500 pièces (pas de remise) tranche |
| Remise calculée dans `LigneCommande` avec accès au client | dépendance inversée | La commande connaît son client ; elle calcule et passe la remise à la ligne |
| Tests nommés `Test1`, `Test2` | habitude | Un nom de test est une phrase : `L_annulation_est_possible_avant_la_preparation_seulement` |

## Points à insister en débriefing

- Le domaine compile sans aucun paquet : c'est le critère qui rend possible
  tout ce qui suit (EF Core au module 4, API au module 5, messaging au
  module 6) sans revenir sur les règles.
- Trois patterns de la section 3 sont maintenant du code que les binômes
  ont écrit : Specification, Result, State (la table de transitions) ; deux
  de la section 4 apparaissent : Money (`decimal` + arrondi bancaire) et
  Layer Supertype (absent volontairement — à discuter : un `EntiteBase`
  n'est utile qu'avec les événements de domaine).
- Lien avec le module 2 : le TP 2 reprend ce projet tel quel et construit
  autour la solution complète (Application, Infrastructure, Api, Worker).

## Bonus

Pour les binômes en avance : ajouter une `Specification<Commande>`
`CommandeUrgenteSpecification` (client grand compte et date de livraison
sous 48 h) et la composer avec `CommandeValidableSpecification` pour
produire une liste des commandes à préparer en priorité — sans toucher à
`Commande`. C'est l'OCP appliqué aux règles métier.
