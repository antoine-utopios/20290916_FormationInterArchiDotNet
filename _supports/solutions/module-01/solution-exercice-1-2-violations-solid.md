# Solution — Exercice 1.2 : Cinq violations SOLID dans `CommandeManager`

> Document formateur — Ne pas distribuer avant la fin de l'exercice.

## Approche pédagogique

L'exercice est calibré pour qu'il y ait **exactement une violation par
lettre**, ce qui force les binômes à chercher hors de la méthode
`ValiderCommande` (l'interface et la sous-classe portent le I et le L).
Pendant le travail, laissez les binômes confondre S et D : c'est la
confusion la plus utile à démêler en correction. En comparaison finale,
faites voter sur « combien d'interfaces ? » — les réponses vont de 2 à 6 ;
la bonne question est « combien de raisons de changer ? ».

Le code « après » ci-dessous compile sur .NET 10 (projet `classlib` +
`Microsoft.Extensions.Logging.Abstractions`) et ses trois tests passent
(xUnit + NSubstitute). Il est volontairement plus simple que le domaine du
TP 1 : il reste au niveau « service applicatif », sans agrégat riche.

## Solution détaillée

### Partie 1 — Le diagnostic

| # | Fragment de code | Principe violé | Ce que cela coûte |
|---|---|---|---|
| 1 | `ValiderCommande` lit la base, calcule la remise, vérifie le stock, met à jour, envoie l'email, écrit un fichier de log | **S** — Single Responsibility | Cinq raisons de changer dans une méthode : un changement de serveur SMTP ou de format de log oblige à retester la validation ; impossible de tester la règle de stock seule |
| 2 | `switch (condition) { case "COL": … case "GC": … }` | **O** — Open / Closed | Chaque nouvelle condition tarifaire rouvre la méthode ; Trame duplique ce `switch` dans quatre écrans, et les grilles divergent (le fil rouge : 3 semaines pour la remise volume) |
| 3 | `CommandeExportManager.ValiderCommande` lève `NotSupportedException` | **L** — Liskov Substitution | Un appelant qui tient un `CommandeManager` ne peut plus appeler `ValiderCommande` sans connaître le type réel ; le polymorphisme devient un piège, et l'écran WinForms a un `if (manager is CommandeExportManager)` quelque part |
| 4 | `ICommandeManager` à six méthodes dont cinq lèvent `NotImplementedException` | **I** — Interface Segregation | Tout consommateur dépend de six méthodes pour en utiliser une ; toute implémentation (ou tout mock de test) doit fournir six méthodes ; l'interface ment sur ce qu'elle sait faire |
| 5 | `new SqlConnection(Cnx)`, `new SmtpClient(...)`, `File.AppendAllText(...)` créés dans la méthode | **D** — Dependency Inversion | Le métier dépend de SQL Server, de SMTP et du système de fichiers ; aucun test sans ces trois infrastructures ; changer de fournisseur d'emails = modifier la règle métier |

### Partie 2 — Le plan de correction

1. **Découper** `ValiderCommande` en un service d'orchestration
   (`ValidationCommandeService`) et quatre dépendances : `IDepotCommandes`
   (charger, enregistrer), `IStockDisponible` (quantité disponible),
   `INotificationClient` (commande validée), `ILogger<T>` (journal).
2. **Remplacer** le `switch` par une interface `IPolitiqueRemise`
   (`ConditionTarifaire`, `Taux`) et une classe par condition, injectées en
   `IEnumerable<IPolitiqueRemise>` ; `SansRemise.Instance` en Null Object.
3. **Remplacer** l'héritage `CommandeExportManager : CommandeManager` par
   une seconde implémentation `ValidationCommandeExport : IValidationCommande`
   qui valide selon le processus douane et **retourne un statut**.
4. **Remplacer** `ICommandeManager` par des interfaces d'une ou deux
   méthodes, orientées consommateur : `IValidationCommande`,
   `IDepotCommandes`, `IStockDisponible`, `INotificationClient`.
5. **Injecter** toutes les dépendances par le constructeur ; aucune classe du
   projet métier ne référence `Microsoft.Data.SqlClient`, `System.Net.Mail`
   ni `System.IO`.

### Partie 3 — La réécriture (code complet, compilable)

Projet `Textinord.Trame.Commandes` (classlib .NET 10) avec le package
`Microsoft.Extensions.Logging.Abstractions`.

`Modele.cs` :

```csharp
namespace Textinord.Trame.Commandes;

public enum StatutCommande { Brouillon, Validee, EnAttenteStock, Annulee }

public sealed record LigneCommande(string ReferenceArticle, int Quantite);

public sealed class Commande
{
    public Commande(int id, string codeClient, string conditionTarifaire, string emailClient, IReadOnlyList<LigneCommande> lignes)
    {
        Id = id;
        CodeClient = codeClient;
        ConditionTarifaire = conditionTarifaire;
        EmailClient = emailClient;
        Lignes = lignes;
    }

    public int Id { get; }

    public string CodeClient { get; }

    public string ConditionTarifaire { get; }

    public string EmailClient { get; }

    public IReadOnlyList<LigneCommande> Lignes { get; }

    public StatutCommande Statut { get; private set; } = StatutCommande.Brouillon;

    public decimal TauxRemise { get; private set; }

    public void Valider(decimal tauxRemise)
    {
        TauxRemise = tauxRemise;
        Statut = StatutCommande.Validee;
    }

    public void MettreEnAttenteStock() => Statut = StatutCommande.EnAttenteStock;
}
```

`Ports.cs` — les interfaces, petites et côté consommateur (I et D) :

```csharp
namespace Textinord.Trame.Commandes;

// ISP : des interfaces petites, orientées client. Chaque consommateur ne voit que ce dont il a besoin.

public interface IValidationCommande
{
    Task<StatutCommande> ValiderAsync(int idCommande, CancellationToken ct = default);
}

public interface IDepotCommandes
{
    Task<Commande?> ChargerAsync(int idCommande, CancellationToken ct = default);

    Task EnregistrerAsync(Commande commande, CancellationToken ct = default);
}

public interface IStockDisponible
{
    Task<int> QuantiteDisponibleAsync(string referenceArticle, CancellationToken ct = default);
}

public interface INotificationClient
{
    Task CommandeValideeAsync(Commande commande, CancellationToken ct = default);
}

public interface IPolitiqueRemise
{
    string ConditionTarifaire { get; }

    decimal Taux { get; }
}
```

`PolitiquesRemise.cs` — le `switch` devient une classe par condition (O) :

```csharp
namespace Textinord.Trame.Commandes;

// OCP : une nouvelle condition tarifaire = une nouvelle classe, pas un case de plus dans un switch.

public sealed class RemiseCollectivite : IPolitiqueRemise
{
    public string ConditionTarifaire => "COL";

    public decimal Taux => 0.10m;
}

public sealed class RemiseHotellerie : IPolitiqueRemise
{
    public string ConditionTarifaire => "HOT";

    public decimal Taux => 0.12m;
}

public sealed class RemiseIndustriel : IPolitiqueRemise
{
    public string ConditionTarifaire => "IND";

    public decimal Taux => 0.15m;
}

public sealed class RemiseGrandCompte : IPolitiqueRemise
{
    public string ConditionTarifaire => "GC";

    public decimal Taux => 0.25m;
}

public sealed class SansRemise : IPolitiqueRemise
{
    public static readonly SansRemise Instance = new();

    public string ConditionTarifaire => "STD";

    public decimal Taux => 0m;
}
```

`ValidationCommandeService.cs` — une seule responsabilité, tout injecté (S et D) :

```csharp
using Microsoft.Extensions.Logging;

namespace Textinord.Trame.Commandes;

// SRP : ce service orchestre la validation, et rien d'autre. Chaque dépendance a sa propre raison de changer.
// DIP : il ne connaît ni SQL Server, ni SMTP, ni le système de fichiers.
public sealed class ValidationCommandeService(
    IDepotCommandes depot,
    IStockDisponible stock,
    IEnumerable<IPolitiqueRemise> politiques,
    INotificationClient notification,
    ILogger<ValidationCommandeService> logger) : IValidationCommande
{
    public async Task<StatutCommande> ValiderAsync(int idCommande, CancellationToken ct = default)
    {
        var commande = await depot.ChargerAsync(idCommande, ct)
                       ?? throw new CommandeIntrouvableException(idCommande);

        var stockOk = true;
        foreach (var ligne in commande.Lignes)
        {
            var disponible = await stock.QuantiteDisponibleAsync(ligne.ReferenceArticle, ct);
            if (disponible < ligne.Quantite)
            {
                stockOk = false;
                break;
            }
        }

        if (!stockOk)
        {
            commande.MettreEnAttenteStock();
            await depot.EnregistrerAsync(commande, ct);
            logger.LogInformation("Commande {Id} en attente de stock", idCommande);
            return commande.Statut;
        }

        var politique = politiques.FirstOrDefault(p => p.ConditionTarifaire == commande.ConditionTarifaire)
                        ?? SansRemise.Instance;

        commande.Valider(politique.Taux);
        await depot.EnregistrerAsync(commande, ct);
        await notification.CommandeValideeAsync(commande, ct);
        logger.LogInformation("Commande {Id} validée, remise {Taux:P0}", idCommande, politique.Taux);

        return commande.Statut;
    }
}

public sealed class CommandeIntrouvableException(int idCommande)
    : Exception($"Commande {idCommande} introuvable.")
{
    public int IdCommande { get; } = idCommande;
}
```

Note sur `CommandeIntrouvableException` : ici, une commande introuvable
alors qu'un écran vient de l'afficher est traitée comme une anomalie
(exception), pas comme un cas métier. Le TP 1 fait le choix inverse avec
`Result` pour les transitions d'état ; les deux sont défendables, ce qui
compte est que le choix soit explicite et cohérent dans un projet.

`ValidationCommandeExport.cs` — la sous-classe qui refusait devient une
implémentation qui honore le contrat (L) :

```csharp
using Microsoft.Extensions.Logging;

namespace Textinord.Trame.Commandes;

// LSP : une commande export se valide aussi, selon un autre processus (dossier douane) ;
// l'implémentation honore le contrat (elle retourne un statut) au lieu de lever NotSupportedException.
public sealed class ValidationCommandeExport(
    IDepotCommandes depot,
    IDossierDouane douane,
    ILogger<ValidationCommandeExport> logger) : IValidationCommande
{
    public async Task<StatutCommande> ValiderAsync(int idCommande, CancellationToken ct = default)
    {
        var commande = await depot.ChargerAsync(idCommande, ct)
                       ?? throw new CommandeIntrouvableException(idCommande);

        var dossierOuvert = await douane.OuvrirDossierAsync(commande, ct);
        if (!dossierOuvert)
        {
            commande.MettreEnAttenteStock();
            await depot.EnregistrerAsync(commande, ct);
            logger.LogWarning("Commande export {Id} : dossier douane refusé", idCommande);
            return commande.Statut;
        }

        commande.Valider(tauxRemise: 0m);
        await depot.EnregistrerAsync(commande, ct);
        logger.LogInformation("Commande export {Id} validée avec dossier douane", idCommande);
        return commande.Statut;
    }
}

public interface IDossierDouane
{
    Task<bool> OuvrirDossierAsync(Commande commande, CancellationToken ct = default);
}
```

Le choix entre `ValidationCommandeService` et `ValidationCommandeExport` se
fait dans le bootstrapper (keyed services, ou une fabrique selon le type de
commande) — jamais par un `if (commande is Export)` dans l'appelant.

### Bonus — Les tests (xUnit + NSubstitute + `NullLogger`)

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Textinord.Trame.Commandes;

namespace Textinord.Trame.Commandes.Tests;

public class ValidationCommandeServiceTests
{
    private readonly IDepotCommandes _depot = Substitute.For<IDepotCommandes>();
    private readonly IStockDisponible _stock = Substitute.For<IStockDisponible>();
    private readonly INotificationClient _notification = Substitute.For<INotificationClient>();

    private ValidationCommandeService Service() => new(
        _depot, _stock, [new RemiseCollectivite(), new RemiseGrandCompte()], _notification,
        NullLogger<ValidationCommandeService>.Instance);

    private static Commande CommandeMairie() => new(
        4212, "C-00042", "COL", "achats@roubaix.fr", [new LigneCommande("VT-4410", 100)]);

    [Fact]
    public async Task Stock_suffisant_valide_avec_la_remise_du_client_et_notifie()
    {
        var commande = CommandeMairie();
        _depot.ChargerAsync(4212).Returns(commande);
        _stock.QuantiteDisponibleAsync("VT-4410").Returns(300);

        var statut = await Service().ValiderAsync(4212);

        Assert.Equal(StatutCommande.Validee, statut);
        Assert.Equal(0.10m, commande.TauxRemise);
        await _notification.Received(1).CommandeValideeAsync(commande);
        await _depot.Received(1).EnregistrerAsync(commande);
    }

    [Fact]
    public async Task Stock_insuffisant_met_en_attente_sans_notifier()
    {
        var commande = CommandeMairie();
        _depot.ChargerAsync(4212).Returns(commande);
        _stock.QuantiteDisponibleAsync("VT-4410").Returns(20);

        var statut = await Service().ValiderAsync(4212);

        Assert.Equal(StatutCommande.EnAttenteStock, statut);
        await _notification.DidNotReceive().CommandeValideeAsync(Arg.Any<Commande>());
    }

    [Fact]
    public async Task Commande_introuvable_leve_une_exception_explicite()
    {
        _depot.ChargerAsync(9999).Returns((Commande?)null);

        var exception = await Assert.ThrowsAsync<CommandeIntrouvableException>(() => Service().ValiderAsync(9999));

        Assert.Equal(9999, exception.IdCommande);
    }
}
```

Résultat attendu de `dotnet test` : 3 tests, 0 échec, sans SQL Server, sans
SMTP, sans fichier.

## Variantes acceptables

1. Regrouper `IDepotCommandes` et `IStockDisponible` dans une même interface
   « lecture des commandes » : acceptable si le binôme argumente qu'elles
   changent pour la même raison (même schéma SQL). On leur fera remarquer
   qu'au module 4 le stock viendra d'une autre source (Cosmos ou cache).
2. Remise calculée par une méthode d'extension sur `Commande` plutôt que
   par des politiques injectées : c'est un `switch` déplacé ; à refuser
   sauf si une table de configuration remplace le `switch`.
3. Garder `ICommandeManager` mais le vider des cinq méthodes non
   implémentées : correct pour le I, mais l'occasion est manquée de nommer
   l'interface par le besoin du consommateur (`IValidationCommande`).
4. Gestion de la commande introuvable par `Result` plutôt que par
   exception : parfaitement acceptable, et cohérent avec le TP 1.

## Erreurs classiques à repérer en correction

| Erreur observée | Cause probable | Comment corriger |
|---|---|---|
| Quatre violations trouvées, pas de L | la sous-classe est lue comme « normale » | Demander : « que se passe-t-il si l'écran reçoit un `CommandeExportManager` par une variable de type `CommandeManager` ? » |
| S et D confondus sur `new SqlConnection` | même ligne, deux principes | Faire formuler chacun en une phrase : « trop de raisons de changer » (S) vs « dépendance vers le concret » (D) |
| Une interface unique `IInfrastructure` à cinq méthodes | ISP compris comme « il faut une interface » | Montrer le mock de test : combien de méthodes faut-il fournir pour tester le stock ? |
| `ILogger` oublié, `File.AppendAllText` conservé | le log est perçu comme « pas du métier » | C'est justement une dépendance technique ; `ILogger<T>` est l'abstraction standard |
| `NotSupportedException` remplacée par `return StatutCommande.Brouillon` | LSP compris comme « ne pas lever » | Le contrat est de **valider** ; retourner un statut arbitraire est un mensonge silencieux, pire qu'une exception |

## Points à insister en débriefing

- SOLID n'est pas une checklist esthétique : chaque violation a un **coût
  observable** chez Textinord (tests impossibles, régressions, duplication,
  écran qui teste le type réel).
- Le nombre d'interfaces se déduit du nombre de raisons de changer, et se
  vérifie avec la question : « qu'est-ce que je dois instancier pour tester
  la règle de stock ? »
- Lien avec le TP 1 : le domaine y va plus loin — la règle de stock et les
  transitions vivent dans l'agrégat `Commande`, le service applicatif ne
  fait plus qu'orchestrer.

## Bonus

Les binômes rapides peuvent enregistrer les deux validations dans un
`Program.cs` avec des keyed services :

```csharp
builder.Services.AddKeyedTransient<IValidationCommande, ValidationCommandeService>("France");
builder.Services.AddKeyedTransient<IValidationCommande, ValidationCommandeExport>("Export");
```

et résoudre par `[FromKeyedServices("Export")] IValidationCommande validation`
dans l'endpoint — vu en détail au module 5.
