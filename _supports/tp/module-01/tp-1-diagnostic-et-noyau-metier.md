# TP 1 — Diagnostic de Trame et noyau métier de Trame 2

> Module : 1 — Architecturer un SI .NET : styles, couches et design patterns
> Durée : 75 min (partie A : 25 min, partie B : 50 min)
> Difficulté : 3 / 5
> Type : TP en binôme, livrable versionné

## Mise en situation

Nous sommes le lundi 7 septembre 2026. Julien Delcourt, architecte, a obtenu
de Nadia Benali un mandat clair : « avant de toucher à une ligne de Trame,
montrez-moi où on va ». Il vous confie deux choses. D'abord, formaliser le
diagnostic de Trame et les trois décisions structurantes sous forme d'ADR,
pour le comité du 21 septembre. Ensuite, poser la première brique de Trame 2 :
le projet `Textinord.Trame.Domain`, un noyau métier sans aucune dépendance
technique, que Sofia Marques et son équipe reprendront dès le module 2 et que
les modules 4 (EF Core), 5 (API) et 6 (messaging) réutiliseront tels quels.

Ce TP est autonome : il fournit son point de départ (généré par `dotnet new`)
et ne dépend d'aucun TP précédent.

## Prérequis

- SDK .NET 10 (`dotnet --version` affiche `10.0.x`)
- Un éditeur : Visual Studio 2026, VS Code + C# Dev Kit, ou Rider
- Accès NuGet (packages xUnit) ou cache NuGet pré-rempli
- Git installé (un dépôt par binôme)

## Rappel des règles métier (FIL-ROUGE)

- Entités : `Client` (code, raison sociale, condition tarifaire), `Article`
  (référence, libellé, famille, prix de base, stock par entrepôt), `Commande`
  (numéro, client, date, statut, lignes), `LigneCommande` (article, quantité,
  prix unitaire négocié, remise), `Entrepot` (code, site), `OrdrePreparation`
  (commande, entrepôt, préparateur, statut).
- Remise client de 0 à 25 % selon la condition tarifaire ; remise volume de
  5 % au-delà de 500 pièces sur une ligne ; les deux se cumulent, plafond 30 %.
- Une commande se valide seulement si chaque ligne a un stock disponible dans
  au moins un entrepôt ; sinon elle passe en `EnAttenteStock`.
- Un ordre de préparation est émis par entrepôt concerné dès la validation ;
  l'expédition est déclarée quand tous les ordres sont clos.
- Statuts : Brouillon → Validee → EnPreparation → Expediee → Facturee ;
  annulation possible avant préparation.
- Numéro de commande `CMD-AAAA-NNNNNN`, unique, séquentiel par année.

## Partie A — Diagnostic et décisions (25 min)

### Étape A1 — Cartographier l'as-is (10 min)

Dans un fichier `docs/diagnostic-trame.md`, produisez un tableau à cinq
colonnes : brique, technologie actuelle, problème constaté, qualité ISO 25010
la plus touchée, cible Trame 2. Une ligne par brique de Trame (application de
gestion, services, messagerie entrepôts, base de données, site B2C, annuaire,
transactions, livraison). Le fil rouge donne la matière ; votre valeur ajoutée
est la colonne « qualité la plus touchée » et la cohérence des cibles.

Point de contrôle A1 : huit lignes, aucune cellule vide, et chaque cible est
une technologie du positionnement 2026 (aucun WCF, MSMQ, Web Forms, COM+ dans
la colonne cible).

### Étape A2 — Trois ADR (15 min)

Dans `docs/adr/`, rédigez trois fichiers en suivant le gabarit ci-dessous :

- `ADR-001-style-architecture.md` — quel style pour Trame 2 (monolithe
  modulaire, micro-services, n-tiers…) et pourquoi, au regard des 6
  développeurs, des 900 commandes / jour et du budget PaaS ;
- `ADR-002-decoupage-en-couches.md` — quelles couches, quelles règles de
  dépendance entre projets, ce qui est interdit (par exemple : `Domain` ne
  référence aucun paquet NuGet technique) ;
- `ADR-003-strategie-de-migration.md` — big bang, Strangler Fig ou
  coexistence longue ; par quelle fonctionnalité commencer ; combien de temps
  les postes WinForms restent supportés.

Gabarit d'un ADR :

```text
# ADR-00N — Titre de la décision
Statut : proposé | accepté | remplacé par ADR-00M
Date : 2026-09-07
Décideurs : (noms et rôles)

## Contexte
Deux à cinq phrases : la situation, les contraintes, ce qui force à décider.

## Options considérées
1. Option A — avantages / inconvénients (deux lignes)
2. Option B — ...
3. Option C — ...

## Décision
L'option retenue, en une phrase, et les deux critères qui ont tranché.

## Conséquences
Ce que la décision impose (positif et négatif), ce qu'elle interdit,
ce qui devra être revu et quand.
```

Point de contrôle A2 : chaque ADR cite au moins deux critères de la grille
des qualités et au moins une contrainte chiffrée de Textinord ; chaque ADR
liste au moins une conséquence négative assumée.

## Partie B — Le noyau métier `Textinord.Trame.Domain` (50 min)

### Étape B1 — Point de départ (5 min)

```bash
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
mkdir tp-1 && cd tp-1
git init
dotnet new sln -n Textinord.Trame --format sln
dotnet new classlib -n Textinord.Trame.Domain -f net10.0 -o src/Textinord.Trame.Domain
dotnet new xunit    -n Textinord.Trame.Domain.Tests -f net10.0 -o tests/Textinord.Trame.Domain.Tests
dotnet sln Textinord.Trame.sln add src/Textinord.Trame.Domain tests/Textinord.Trame.Domain.Tests
dotnet add tests/Textinord.Trame.Domain.Tests reference src/Textinord.Trame.Domain
rm src/Textinord.Trame.Domain/Class1.cs tests/Textinord.Trame.Domain.Tests/UnitTest1.cs
dotnet build
```

Créez un `.gitignore` (`bin/`, `obj/`, `.vs/`, `*.user`) et faites un premier
commit. Activez `Nullable`, `ImplicitUsings` et `TreatWarningsAsErrors` dans le
projet `Domain`.

Point de contrôle B1 : `dotnet build` réussit, `git log` montre un commit, le
projet `Domain` ne contient aucun `PackageReference`.

### Étape B2 — Les briques communes : `Result` et `Specification` (10 min)

Dans `Communs/` :

- `Erreur` : un record `(Code, Message)` ;
- `Result` et `Result<T>` : `EstSucces`, `EstEchec`, `Erreur`, fabriques
  `Ok(...)` et `Echec(...)` ; lire `Valeur` sur un échec lève une
  `InvalidOperationException` (c'est un bug, pas un cas métier) ;
- `Specification<T>` : classe abstraite avec `EstSatisfaitePar(T)` et les
  combinateurs `Et`, `Ou`, `Non`.

Point de contrôle B2 : un test vérifie qu'un `Result<int>` en échec porte son
code d'erreur et refuse de livrer sa valeur ; un test vérifie la composition
`Et` / `Ou` / `Non` sur deux spécifications simples.

### Étape B3 — Le modèle : `Client`, `Article`, `Entrepot`, `ConditionTarifaire` (10 min)

- `ConditionTarifaire` : objet-valeur (record) avec un code et un taux de
  0 à 25 %, validé à la construction ; fournissez des valeurs prédéfinies
  (`Standard` 0 %, `Collectivite` 10 %, `Hotellerie` 12 %, `Industriel` 15 %,
  `GrandCompte` 25 %).
- `Client` : code, raison sociale, condition tarifaire (modifiable par une
  méthode explicite, pas par un setter public).
- `Entrepot` : record avec code, site et une priorité (Roubaix avant Lesquin).
- `Article` : référence, libellé, famille, prix de base strictement positif,
  stock par entrepôt ; une méthode `EntrepotPouvantServir(quantite)` renvoie
  le premier entrepôt (par priorité) dont le stock couvre la quantité, ou
  `null`.

Point de contrôle B3 : construire une `ConditionTarifaire` à 26 % lève une
`ArgumentOutOfRangeException` ; le test est écrit.

### Étape B4 — La règle de remise (5 min)

Dans `Tarification/`, une fonction pure `CalculRemise.Calculer(tauxClient,
quantite)` qui retourne un record `Remise(TauxClient, TauxVolume,
TauxApplique)` avec une propriété `Plafonnee`. Seuil de volume : la 501e
pièce déclenche les 5 %. Plafond : 30 %.

Point de contrôle B4 : quatre tests — remise client seule, remise volume à
500 et 501 pièces, cumul grand compte + volume = 30 %, écrêtage d'un cumul
supérieur à 30 %.

### Étape B5 — L'agrégat `Commande` et sa machine à états (15 min)

Dans `Commandes/` :

- `NumeroCommande` : objet-valeur `CMD-AAAA-NNNNNN` avec un `Parser(string)`
  qui retourne un `Result<NumeroCommande>` ; `IGenerateurNumeroCommande` et une
  implémentation en mémoire, séquentielle par année.
- `StatutCommande` : `Brouillon`, `EnAttenteStock`, `Validee`,
  `EnPreparation`, `Expediee`, `Facturee`, `Annulee`.
- `LigneCommande` : construite uniquement par la commande ; `MontantNet`
  arrondi à deux décimales.
- `OrdrePreparation` : commande, entrepôt, lignes à préparer, préparateur,
  statut `Emis` → `EnCours` → `Clos`.
- `Commande` :
  - `AjouterLigne(article, quantite, prixNegocie?)` retourne un
    `Result<LigneCommande>` (échec si quantité nulle, si la commande n'est plus
    modifiable) et calcule la remise avec la condition du client ;
  - `Valider()` utilise une `CommandeValidableSpecification` (composée d'une
    `StockDisponibleSpecification` par ligne) : passe `Validee` et émet un
    `OrdrePreparation` par entrepôt concerné, ou passe `EnAttenteStock` ;
  - `DemarrerPreparation()`, `Expedier()` (seulement si tous les ordres sont
    clos), `Facturer()`, `Annuler()` (seulement avant la préparation) ;
  - la table des transitions autorisées est écrite **une seule fois**, sous
    forme de dictionnaire, et une transition interdite retourne un `Result` en
    échec avec le code `TRANSITION_INTERDITE` — jamais une exception.

Point de contrôle B5 : au moins huit tests au total dans le projet, dont :
validation avec ordres par entrepôt (une commande dont une ligne est servie par
Lesquin et une autre par Roubaix produit deux ordres) ; passage en attente de
stock ; annulation refusée après préparation ; expédition refusée tant qu'un
ordre est ouvert ; `dotnet test` vert.

### Étape B6 — Livraison (5 min)

```bash
dotnet build
dotnet test
git add -A && git commit -m "TP1 : noyau métier Textinord.Trame.Domain"
```

## Livrable

Un dépôt Git contenant :

- `docs/diagnostic-trame.md` et `docs/adr/ADR-001…003.md` ;
- `Textinord.Trame.sln`, `src/Textinord.Trame.Domain/`,
  `tests/Textinord.Trame.Domain.Tests/`, `.gitignore` ;
- `dotnet build` sans avertissement, `dotnet test` avec au moins huit tests
  verts.

## Dépannage

| Symptôme | Cause probable | Correction |
|---|---|---|
| `error CA1000` (membre statique sur type générique) sur `Result<T>.Ok` | analyzers `latest-recommended` | ajouter `<NoWarn>$(NoWarn);CA1000</NoWarn>` au projet, avec un commentaire : les fabriques statiques sont l'usage idiomatique du pattern |
| `error CA1305` sur `int.Parse` | culture non spécifiée | `int.Parse(texte, CultureInfo.InvariantCulture)` |
| `warning CA1707` sur les noms de tests avec underscores | analyzer de nommage | désactiver CA1707 dans le projet de tests uniquement |
| Le domaine « a besoin » d'EF Core pour les collections | mauvais réflexe | une `List<T>` privée exposée en `IReadOnlyList<T>` suffit ; la persistance arrive au module 4 |
| `dotnet test` ne trouve aucun test | classe ou méthode non publique | les classes de tests et les méthodes `[Fact]` sont `public` |
| `NumeroCommande` avec `[GeneratedRegex]` ne compile pas | classe non `partial` | `public sealed partial record NumeroCommande` |

## Grille d'évaluation

| Critère | Points |
|---|---|
| A1 — Cartographie complète, cibles conformes au positionnement 2026 | 2 |
| A2 — Trois ADR structurés, critères et contraintes cités, conséquences négatives assumées | 3 |
| B — Le projet `Domain` ne référence aucun paquet technique, compile sans avertissement | 2 |
| B — Règle de remise correcte (seuil 501, cumul, plafond 30 %) et testée | 2 |
| B — Machine à états écrite une fois, transitions interdites en `Result`, annulation et expédition conformes | 3 |
| B — Specification composable utilisée pour la validation de stock ; ordres de préparation par entrepôt | 3 |
| B — Au moins huit tests xUnit lisibles, nommés comme des phrases, `dotnet test` vert | 3 |
| Dépôt Git propre (`.gitignore`, commits explicites) | 2 |
| **Total** | **20** |

## Teardown

Rien à supprimer : le dépôt sert de base au TP 2 (structuration de la
solution complète) et au TP 4 (persistance EF Core). Si vous avez travaillé
sur un poste partagé, archivez le dépôt (`git bundle create tp-1.bundle --all`)
et supprimez les dossiers `bin/` et `obj/`.
