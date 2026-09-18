# Exercice 6.2 — Tests unitaires avec NSubstitute

> Module : 6 — Legacy, messaging, identité d'entreprise et industrialisation
> Durée estimée : 25 min
> Difficulté : 3 / 5
> Type : exercice de code individuel, sur poste

## Objectifs pédagogiques

À la fin de cet exercice, vous serez capable de :

- Isoler un service métier de ses dépendances avec des substituts NSubstitute (`Substitute.For<T>()`, `Returns`, `ThrowsAsync`)
- Vérifier des interactions, pas seulement des valeurs : `Received`, `DidNotReceive`, `ReceivedWithAnyArgs`
- Rendre le temps testable en substituant `TimeProvider`
- Écrire des tests dont chacun échouerait si la règle qu'il protège disparaissait

## Prérequis

- SDK .NET 10 installé (`dotnet --version` affiche `10.0.x`)
- Un éditeur (Visual Studio 2026 / 2022, VS Code avec C# Dev Kit, Rider)
- Avoir suivi la section 5 du module 6 (pyramide des tests, xUnit, NSubstitute)

## Contexte

Sofia Marques a extrait de la classe `CommandeManager` de Trame (module 1, exercice 1.2) un service `ValidationCommandeService` propre : il ne parle qu'à des interfaces. Le pipeline de Trame 2 refuse désormais toute pull request qui fait baisser la couverture des règles métier. Deux tests existent ; il en manque sept pour couvrir les règles Textinord :

1. le client doit exister et être actif ;
2. une commande a au moins une ligne et des quantités strictement positives ;
3. chaque ligne doit avoir un entrepôt avec du stock, sinon la commande passe en attente de stock et l'administration des ventes (ADV) est alertée pour chaque rupture ;
4. la remise est client + volume (5 % au-delà de 500 pièces), plafonnée à 30 % ;
5. la date de validation vient de l'horloge injectée.

## Mise en place (5 min)

Créez la solution et les deux projets :

```bash
mkdir exercice-6-2 && cd exercice-6-2
dotnet new sln -n Textinord.Trame.Validation --format sln
dotnet new classlib -n Textinord.Trame.Validation -o src/Textinord.Trame.Validation
dotnet new xunit -n Textinord.Trame.Validation.Tests -o tests/Textinord.Trame.Validation.Tests
dotnet sln add src/Textinord.Trame.Validation/Textinord.Trame.Validation.csproj
dotnet sln add tests/Textinord.Trame.Validation.Tests/Textinord.Trame.Validation.Tests.csproj
dotnet add tests/Textinord.Trame.Validation.Tests reference src/Textinord.Trame.Validation
```

Supprimez `Class1.cs` et `UnitTest1.cs`. Remplacez le contenu des deux fichiers projet par ceux-ci, pour fixer les versions et activer les `using` globaux :

`src/Textinord.Trame.Validation/Textinord.Trame.Validation.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Textinord.Trame.Validation.Tests" />
  </ItemGroup>
</Project>
```

`tests/Textinord.Trame.Validation.Tests/Textinord.Trame.Validation.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="NSubstitute" Version="6.2.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/Textinord.Trame.Validation/Textinord.Trame.Validation.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="NSubstitute" />
  </ItemGroup>
</Project>
```

## Le service fourni

Créez ces quatre fichiers dans `src/Textinord.Trame.Validation/`. Ils ne sont pas à modifier : l'exercice porte sur les tests.

`Modele.cs`

```csharp
namespace Textinord.Trame.Validation;

public enum StatutCommande
{
    Brouillon,
    Validee,
    EnAttenteStock,
    Refusee
}

public sealed record Client(string Code, string RaisonSociale, decimal TauxRemise, bool Actif);

public sealed class LigneCommande(string reference, int quantite, decimal prixUnitaire)
{
    public string Reference { get; } = reference;

    public int Quantite { get; } = quantite;

    public decimal PrixUnitaire { get; } = prixUnitaire;

    public decimal TauxRemise { get; internal set; }

    public string? Entrepot { get; internal set; }

    public decimal MontantNet => Math.Round(Quantite * PrixUnitaire * (1 - TauxRemise), 2, MidpointRounding.AwayFromZero);
}

public sealed class Commande(string numero, string codeClient)
{
    public string Numero { get; } = numero;

    public string CodeClient { get; } = codeClient;

    public List<LigneCommande> Lignes { get; } = [];

    public StatutCommande Statut { get; internal set; } = StatutCommande.Brouillon;

    public DateTimeOffset? ValideeLe { get; internal set; }
}

public sealed record ResultatValidation(StatutCommande Statut, IReadOnlyList<string> Motifs)
{
    public bool EstValidee => Statut == StatutCommande.Validee;
}
```

`Ports.cs`

```csharp
namespace Textinord.Trame.Validation;

/// <summary>Référentiel clients (table SQL, cache Redis… peu importe ici : c'est un port).</summary>
public interface IReferentielClients
{
    Task<Client?> TrouverAsync(string code, CancellationToken ct = default);
}

/// <summary>Stock par entrepôt : renvoie les codes d'entrepôt capables de servir la quantité, par ordre de préférence.</summary>
public interface IStockDisponible
{
    Task<IReadOnlyList<string>> EntrepotsDisponiblesAsync(string reference, int quantite, CancellationToken ct = default);
}

/// <summary>Alerte l'administration des ventes (mail Teams, ticket…) d'une rupture bloquant une commande.</summary>
public interface INotificateurAdv
{
    Task SignalerRuptureAsync(string numeroCommande, string reference, CancellationToken ct = default);
}
```

`RegleRemise.cs`

```csharp
namespace Textinord.Trame.Validation;

/// <summary>Remise client (0 à 25 %) + 5 % au-delà de 500 pièces, plafond 30 %.</summary>
public static class RegleRemise
{
    public static decimal Calculer(decimal tauxClient, int quantite)
    {
        if (tauxClient is < 0 or > 0.25m)
        {
            throw new ArgumentOutOfRangeException(nameof(tauxClient), tauxClient, "Taux client entre 0 et 25 %.");
        }

        var taux = tauxClient + (quantite > 500 ? 0.05m : 0m);
        return Math.Min(taux, 0.30m);
    }
}
```

`ValidationCommandeService.cs`

```csharp
namespace Textinord.Trame.Validation;

/// <summary>
/// Validation d'une commande Textinord :
///   1. le client existe et est actif, sinon refus ;
///   2. au moins une ligne, quantités strictement positives, sinon refus ;
///   3. chaque ligne doit avoir un entrepôt avec du stock ; sinon la commande passe
///      en attente de stock et l'ADV est alertée pour chaque rupture ;
///   4. la remise (client + volume, plafond 30 %) est appliquée ligne par ligne ;
///   5. la date de validation vient de l'horloge injectée (testable).
/// </summary>
public sealed class ValidationCommandeService(
    IReferentielClients clients,
    IStockDisponible stock,
    INotificateurAdv notificateur,
    TimeProvider horloge)
{
    public async Task<ResultatValidation> ValiderAsync(Commande commande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(commande);

        var client = await clients.TrouverAsync(commande.CodeClient, ct);
        if (client is null)
        {
            return Refuser(commande, $"Client inconnu : {commande.CodeClient}.");
        }

        if (!client.Actif)
        {
            return Refuser(commande, $"Client inactif : {commande.CodeClient}.");
        }

        if (commande.Lignes.Count == 0)
        {
            return Refuser(commande, "Une commande doit contenir au moins une ligne.");
        }

        var quantitesInvalides = commande.Lignes
            .Where(l => l.Quantite <= 0)
            .Select(l => $"Quantité invalide sur {l.Reference} : {l.Quantite}.")
            .ToArray();
        if (quantitesInvalides.Length > 0)
        {
            return Refuser(commande, quantitesInvalides);
        }

        var ruptures = new List<string>();
        foreach (var ligne in commande.Lignes)
        {
            var entrepots = await stock.EntrepotsDisponiblesAsync(ligne.Reference, ligne.Quantite, ct);
            if (entrepots.Count == 0)
            {
                ruptures.Add(ligne.Reference);
                await notificateur.SignalerRuptureAsync(commande.Numero, ligne.Reference, ct);
                continue;
            }

            ligne.Entrepot = entrepots[0];
        }

        if (ruptures.Count > 0)
        {
            commande.Statut = StatutCommande.EnAttenteStock;
            return new ResultatValidation(
                StatutCommande.EnAttenteStock,
                ruptures.Select(r => $"Rupture de stock : {r}.").ToList());
        }

        foreach (var ligne in commande.Lignes)
        {
            ligne.TauxRemise = RegleRemise.Calculer(client.TauxRemise, ligne.Quantite);
        }

        commande.Statut = StatutCommande.Validee;
        commande.ValideeLe = horloge.GetUtcNow();
        return new ResultatValidation(StatutCommande.Validee, []);
    }

    private static ResultatValidation Refuser(Commande commande, params string[] motifs)
    {
        commande.Statut = StatutCommande.Refusee;
        return new ResultatValidation(StatutCommande.Refusee, motifs);
    }
}
```

## Les deux tests fournis

Créez `tests/Textinord.Trame.Validation.Tests/ValidationCommandeServiceTests.cs` avec ce contenu. Le constructeur prépare les substituts et deux aides (`CommandeDe`, `StockPartout`) que vos tests réutiliseront.

```csharp
using NSubstitute.ExceptionExtensions;

namespace Textinord.Trame.Validation.Tests;

public sealed class ValidationCommandeServiceTests
{
    private static readonly DateTimeOffset Instant = new(2026, 9, 7, 9, 30, 0, TimeSpan.FromHours(2));

    private static readonly Client HotelLeBeffroi = new("C-0001", "Hôtel Le Beffroi, Lille", 0.10m, Actif: true);
    private static readonly Client ChantiersVandamme = new("C-0117", "Chantiers Vandamme SA", 0.25m, Actif: true);
    private static readonly Client ConfectionDubrulle = new("C-0900", "Confection Dubrulle", 0.05m, Actif: false);

    private readonly IReferentielClients _clients = Substitute.For<IReferentielClients>();
    private readonly IStockDisponible _stock = Substitute.For<IStockDisponible>();
    private readonly INotificateurAdv _notificateur = Substitute.For<INotificateurAdv>();
    private readonly TimeProvider _horloge = Substitute.For<TimeProvider>();
    private readonly ValidationCommandeService _service;

    public ValidationCommandeServiceTests()
    {
        _clients.TrouverAsync(HotelLeBeffroi.Code, Arg.Any<CancellationToken>()).Returns(HotelLeBeffroi);
        _clients.TrouverAsync(ChantiersVandamme.Code, Arg.Any<CancellationToken>()).Returns(ChantiersVandamme);
        _clients.TrouverAsync(ConfectionDubrulle.Code, Arg.Any<CancellationToken>()).Returns(ConfectionDubrulle);
        _horloge.GetUtcNow().Returns(Instant);
        _service = new ValidationCommandeService(_clients, _stock, _notificateur, _horloge);
    }

    private static Commande CommandeDe(string codeClient, params (string Reference, int Quantite)[] lignes)
    {
        var commande = new Commande("CMD-2026-000123", codeClient);
        commande.Lignes.AddRange(lignes.Select(l => new LigneCommande(l.Reference, l.Quantite, 10m)));
        return commande;
    }

    private void StockPartout()
    {
        _stock.EntrepotsDisponiblesAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { "RBX", "LSQ" });
    }

    [Fact]
    public async Task Refuse_une_commande_dont_le_client_est_inactif()
    {
        var commande = CommandeDe(ConfectionDubrulle.Code, ("VT-1001", 10));

        var resultat = await _service.ValiderAsync(commande);

        Assert.Equal(StatutCommande.Refusee, resultat.Statut);
        Assert.Contains("inactif", resultat.Motifs.Single());
        Assert.Equal(StatutCommande.Refusee, commande.Statut);
    }

    [Fact]
    public async Task Valide_une_commande_dont_toutes_les_lignes_sont_en_stock()
    {
        StockPartout();
        var commande = CommandeDe(HotelLeBeffroi.Code, ("VT-1001", 10), ("LH-3300", 40));

        var resultat = await _service.ValiderAsync(commande);

        Assert.True(resultat.EstValidee);
        Assert.Empty(resultat.Motifs);
        Assert.Equal(StatutCommande.Validee, commande.Statut);
    }
}
```

Vérifiez le point de départ : `dotnet test` doit afficher deux tests réussis.

## Énoncé

Chaque test suit le même plan : préparer les substituts, appeler `ValiderAsync`, vérifier le résultat **et** les interactions. Un test doit échouer si la règle qu'il protège est retirée du service : avant de passer au suivant, commentez la règle dans le service, relancez, constatez le rouge, rétablissez.

### Partie 1 — Les refus (8 min)

1. `Refuse_un_client_inconnu_sans_interroger_le_stock` : `TrouverAsync("C-9999", …)` renvoie `null`. Attendu : statut `Refusee`, motif contenant « Client inconnu », et **aucun** appel à `EntrepotsDisponiblesAsync`.
2. `Refuse_une_ligne_a_quantite_nulle_ou_negative_sans_interroger_le_stock` : deux lignes, quantités `0` et `-3`. Attendu : `Refusee`, deux motifs contenant « Quantité invalide », aucun appel au stock.
3. `Refuse_une_commande_sans_ligne` : `Refusee`, un motif contenant « au moins une ligne ».

### Partie 2 — Le stock (9 min)

4. `Passe_en_attente_de_stock_et_alerte_l_adv_pour_chaque_rupture` : trois lignes, `VT-1001` disponible à Roubaix, `VT-1050` et `EPI-2040` en rupture (liste vide). Attendu : statut `EnAttenteStock` sur le résultat et sur la commande, `ValideeLe` nul, motifs `Rupture de stock : VT-1050.` puis `Rupture de stock : EPI-2040.` dans cet ordre, **exactement une** notification par rupture avec le bon numéro de commande, et deux notifications au total.
5. `Affecte_le_premier_entrepot_disponible_a_chaque_ligne` : `VT-1001` disponible à `LSQ` puis `RBX`, `LH-3300` disponible à `RBX` seulement. Attendu : les lignes portent `LSQ` puis `RBX`, et le stock a été interrogé une fois par ligne avec la bonne quantité.

### Partie 3 — Remise, horloge, exception (8 min)

6. `Applique_la_remise_client_plus_volume_plafonnee_a_30_pour_cent` : une **théorie** (`[Theory]` + `[InlineData]`) sur trois cas — Hôtel Le Beffroi 100 pièces → 10 %, Hôtel Le Beffroi 600 pièces → 15 %, Chantiers Vandamme 600 pièces → 30 %.
7. `Date_la_validation_avec_l_horloge_injectee` : `ValideeLe` vaut exactement `Instant`, et `GetUtcNow` a été appelé une fois.
8. `Propage_l_exception_du_referentiel_et_laisse_la_commande_en_brouillon` : le référentiel lève `TimeoutException("SQL Server injoignable")`. Attendu : `ValiderAsync` propage la même exception (`Assert.ThrowsAsync<TimeoutException>`), la commande reste `Brouillon`, l'ADV n'a pas été notifiée.

Le huitième compte pour le bonus si vous êtes à court de temps ; les sept premiers sont attendus.

Résultat attendu : `dotnet test` affiche au moins neuf tests réussis (deux fournis, sept écrits), et chacun est passé au rouge une fois pendant l'exercice.

## Indices (à consulter si bloqué)

<details>
<summary>Indice 1 — Renvoyer null depuis un substitut</summary>

`_clients.TrouverAsync("C-9999", Arg.Any<CancellationToken>()).Returns((Client?)null);` — le transtypage explicite évite l'ambiguïté entre les surcharges de `Returns`.

</details>

<details>
<summary>Indice 2 — Vérifier qu'un appel n'a pas eu lieu</summary>

`await _stock.DidNotReceiveWithAnyArgs().EntrepotsDisponiblesAsync(default!, default, default);` ignore les arguments. Pour vérifier un appel précis : `await _notificateur.Received(1).SignalerRuptureAsync("CMD-2026-000123", "VT-1050", Arg.Any<CancellationToken>());`.

</details>

<details>
<summary>Indice 3 — Faire lever une exception à une méthode asynchrone</summary>

`using NSubstitute.ExceptionExtensions;` puis `_clients.TrouverAsync("C-0001", Arg.Any<CancellationToken>()).ThrowsAsync(new TimeoutException("SQL Server injoignable"));`. Avec `Throws` (sans `Async`), l'exception serait levée à l'appel plutôt qu'à l'`await` ; pour ce service, les deux formes font passer le test, mais `ThrowsAsync` reflète le comportement réel d'un appel réseau.

</details>

<details>
<summary>Indice 4 — Une théorie avec des décimales</summary>

`[InlineData("C-0001", 600, 0.15)]` transmet un `double` ; déclarez le paramètre en `double attendu` et comparez avec `(decimal)attendu`, ou passez par `[MemberData]` avec des `decimal`.

</details>

<details>
<summary>Indice 5 — Un test qui ne passe pas au rouge</summary>

Si le test reste vert après avoir commenté la règle, il ne vérifie pas la bonne chose : il manque une assertion sur l'interaction (`Received`) ou sur l'état de la commande (`commande.Statut`), pas seulement sur le résultat renvoyé.

</details>

## Pour aller plus loin (bonus)

- `Consulte_le_client_avant_le_stock` : avec `Received.InOrder(async () => { … })`, vérifiez que `TrouverAsync` est appelé avant `EntrepotsDisponiblesAsync`.
- Remplacez les clients écrits à la main par `Bogus` (`dotnet add package Bogus`) : un `Faker<Client>` qui génère des raisons sociales et des taux entre 0 et 25 %, et une théorie qui vérifie que la remise ne dépasse jamais 30 % sur cent clients générés avec une graine fixe (`UseSeed(42)`).
- Mesurez la couverture : `dotnet test --collect:"XPlat Code Coverage"` puis ouvrez `coverage.cobertura.xml` ; quelle ligne du service n'est couverte par aucun test ?
