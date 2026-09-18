# Solution — Exercice 4.2 : Optimiser un DbContext et trois requêtes de Trame 2

> Document formateur — Ne pas distribuer avant la fin de l'exercice.

## Approche pédagogique

Les quatre défauts sont volontairement caricaturaux pour être trouvés en huit minutes ; la valeur de l'exercice est dans la partie 3, la preuve par le SQL généré. Insistez pour que chaque binôme lance réellement `LogTo` (SQLite en mémoire suffit) et compte les requêtes : un N+1 se comprend en le lisant, il se retient en le voyant défiler. Le cinquième point (la configuration) sert de transition vers le module 5 (injection de dépendances, configuration, Key Vault).

## Solution détaillée

### Partie 1 — Diagnostic

| Défaut | Où | Explication |
|---|---|---|
| N+1 (en réalité 1 + 2N) | `ListeCommandesValidees` | une requête pour les commandes, puis dans la boucle deux `await` par commande : `Clients.SingleAsync` et `LignesCommande.Where(...)`. Pour 900 commandes : 1 801 requêtes. Le symptôme « 901 requêtes » mesuré par Sofia ne comptait que les clients |
| Suivi inutile | `ExportCommandesFacturees` | la méthode retourne un `string` ; les 900 commandes, 1 200 clients et 3 000 lignes sont pourtant suivies par le change tracker (snapshots, identity map) : c'est la mémoire et le temps mesurés. En prime, deux `Include` dont une collection : jointure unique, chaque ligne de commande répète les colonnes du client (explosion cartésienne modérée) |
| Projection manquante | `SuiviClient` | l'écran affiche numéro, date, statut ; le code charge `Commandes`, `Clients` et `LignesCommande` (trois tables), matérialise trois types d'entités, pour n'en garder que trois colonnes |
| Index absent | `Commandes` | la requête la plus fréquente filtre sur `Statut` (égalité) et trie sur `Date` : il faut un index composé (`Statut`, `Date`), dans cet ordre. L'index sur `Numero` (unique) ne sert pas ici ; un index sur `Statut` seul laisse un tri en mémoire |
| Configuration | `OnConfiguring` | chaîne de connexion avec mot de passe dans le code ; `UseLazyLoadingProxies()` (source de N+1 silencieux et de proxys dans le domaine) ; `EnableSensitiveDataLogging()` et `LogTo(Console.WriteLine)` en production (fuite de données dans les journaux, coût) |

### Partie 2 — Code corrigé

#### `TrameDbContext.cs` (après)

```csharp
using Microsoft.EntityFrameworkCore;
using Textinord.Trame.Domain;

namespace Textinord.Trame.Infrastructure.Persistence;

public sealed class TrameDbContext(DbContextOptions<TrameDbContext> options) : DbContext(options)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Commande> Commandes => Set<Commande>();
    public DbSet<LigneCommande> LignesCommande => Set<LigneCommande>();

    // Plus de OnConfiguring : la configuration est injectée (voir l'enregistrement ci-dessous).

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Commande>(b =>
        {
            b.ToTable("Commandes");
            b.HasKey(c => c.Id);
            b.Property(c => c.Numero).HasMaxLength(15).IsRequired();
            b.HasIndex(c => c.Numero).IsUnique();
            b.Property(c => c.Statut).HasConversion<string>().HasMaxLength(20);

            // L'index qui manquait : égalité sur Statut, tri sur Date.
            b.HasIndex(c => new { c.Statut, c.Date }).HasDatabaseName("IX_Commandes_Statut_Date");

            b.HasOne(c => c.Client).WithMany().HasForeignKey(c => c.ClientId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(c => c.Lignes).WithOne().HasForeignKey("CommandeId").IsRequired().OnDelete(DeleteBehavior.Cascade);
            b.Navigation(c => c.Lignes).HasField("_lignes");
            b.OwnsOne(c => c.Metadonnees, m => m.ToJson());
            b.Property(c => c.Version).IsConcurrencyToken();
            b.Ignore(c => c.TotalHt);
        });

        modelBuilder.Entity<LigneCommande>(b =>
        {
            b.ToTable("LignesCommande");
            b.HasKey(l => l.Id);
            b.Property(l => l.ReferenceArticle).HasMaxLength(30).IsRequired();
            b.Property(l => l.PrixUnitaire).HasPrecision(18, 2);
            b.Property(l => l.RemisePourcent).HasPrecision(5, 2);
            b.HasOne<Article>().WithMany().HasForeignKey(l => l.ArticleId).OnDelete(DeleteBehavior.Restrict);
            b.Ignore(l => l.MontantHt);
        });

        modelBuilder.Entity<Client>(b =>
        {
            b.ToTable("Clients");
            b.HasKey(c => c.Id);
            b.Property(c => c.Code).HasMaxLength(20).IsRequired();
            b.HasIndex(c => c.Code).IsUnique();
            b.Property(c => c.RaisonSociale).HasMaxLength(200).IsRequired();
            b.Property(c => c.ConditionTarifaire).HasConversion<string>().HasMaxLength(30);
        });

        modelBuilder.Entity<Article>(b =>
        {
            b.ToTable("Articles");
            b.HasKey(a => a.Id);
            b.Property(a => a.Reference).HasMaxLength(30).IsRequired();
            b.HasIndex(a => a.Reference).IsUnique();
            b.Property(a => a.PrixBase).HasPrecision(18, 2);
            b.OwnsMany(a => a.Stocks, s =>
            {
                s.ToTable("StocksArticle");
                s.WithOwner().HasForeignKey("ArticleId");
                s.Property<int>("Id");
                s.HasKey("Id");
            });
            b.Navigation(a => a.Stocks).HasField("_stocks");
        });
    }
}
```

Enregistrement dans l'hôte (`Program.cs` de l'API, module 5) : la chaîne vient de la configuration (`appsettings`, variable d'environnement, Key Vault), l'authentification Entra ID évite tout mot de passe, la journalisation sensible est réservée au développement.

```csharp
builder.Services.AddDbContextPool<TrameDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Trame2"),   // "Server=tcp:sql-trame.database.windows.net;Database=Trame2;Authentication=Active Directory Default;"
        sql => sql.EnableRetryOnFailure(maxRetryCount: 5));

    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging()
               .LogTo(Console.WriteLine, LogLevel.Information);
    }
});
```

#### `RequetesCommandes.cs` (après)

```csharp
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Textinord.Trame.Domain;
using Textinord.Trame.Infrastructure.Persistence;

namespace Textinord.Trame.Infrastructure.Lecture;

public sealed record LigneListeAdv(string Numero, string Client, int NombreLignes, decimal TotalHt);

public sealed record LigneSuiviClient(string Numero, DateTime Date, string Statut);

public sealed record LigneExport(
    string Numero, string Client, string Article, int Quantite, decimal PrixUnitaire, decimal Remise)
{
    public decimal MontantHt => Math.Round(Quantite * PrixUnitaire * (1 - Remise / 100m), 2);
}

public sealed class RequetesCommandes(TrameDbContext db)
{
    // Une seule requête SQL : jointure client, sous-requêtes COUNT et SUM, aucune entité matérialisée.
    public async Task<List<LigneListeAdv>> ListeCommandesValidees(DateTime jour, CancellationToken ct)
    {
        var fin = jour.AddDays(1);
        return await db.Commandes
            .Where(c => c.Statut == StatutCommande.Validee && c.Date >= jour && c.Date < fin)
            .OrderBy(c => c.Date)
            .Select(c => new LigneListeAdv(
                c.Numero,
                c.Client!.RaisonSociale,
                c.Lignes.Count,
                c.Lignes.Sum(l => l.Quantite * l.PrixUnitaire * (1 - l.RemisePourcent / 100m))))
            .ToListAsync(ct);
    }

    // Projection plate : une ligne SQL par ligne de commande, rien n'est suivi, le total se calcule sur le DTO.
    public async Task<string> ExportCommandesFacturees(int annee, int mois, CancellationToken ct)
    {
        var debut = new DateTime(annee, mois, 1, 0, 0, 0, DateTimeKind.Utc);
        var fin = debut.AddMonths(1);

        var lignes = await db.Commandes
            .Where(c => c.Statut == StatutCommande.Facturee && c.Date >= debut && c.Date < fin)
            .OrderBy(c => c.Numero)
            .SelectMany(c => c.Lignes, (c, l) => new LigneExport(
                c.Numero, c.Client!.RaisonSociale, l.ReferenceArticle, l.Quantite, l.PrixUnitaire, l.RemisePourcent))
            .ToListAsync(ct);

        var csv = new StringBuilder("Numero;Client;Article;Quantite;PrixUnitaire;Remise;MontantHt\n");
        foreach (var l in lignes)
        {
            csv.Append(CultureInfo.InvariantCulture,
                $"{l.Numero};{l.Client};{l.Article};{l.Quantite};{l.PrixUnitaire};{l.Remise};{l.MontantHt}\n");
        }
        return csv.ToString();
    }

    // Trois colonnes lues, aucune jointure, aucune entité : la projection est la requête.
    public async Task<List<LigneSuiviClient>> SuiviClient(int clientId, CancellationToken ct)
    {
        var lignes = await db.Commandes
            .Where(c => c.ClientId == clientId)
            .OrderByDescending(c => c.Date)
            .Take(20)
            .Select(c => new { c.Numero, c.Date, c.Statut })
            .ToListAsync(ct);

        return lignes
            .Select(l => new LigneSuiviClient(l.Numero, l.Date, l.Statut.ToString()))
            .ToList();
    }
}
```

Variante acceptable pour l'export, si le binôme tient à garder les entités : `.AsNoTracking().AsSplitQuery().Include(c => c.Client).Include(c => c.Lignes)`. Deux requêtes au lieu d'une, aucune entité suivie, plus de répétition des colonnes client ; mais 4 200 objets sont quand même matérialisés pour produire une chaîne. La projection reste préférable.

Note SQLite : `Sum` sur `decimal` n'est pas traduit par le provider SQLite (la valeur est stockée en texte). Pour tester la première requête sur SQLite, projeter `(double)l.Quantite * (double)l.PrixUnitaire * (1 - (double)l.RemisePourcent / 100)` puis convertir ; sur SQL Server et PostgreSQL, la version `decimal` ci-dessus se traduit directement.

#### Migration de l'index

Générée par `dotnet dotnet-ef migrations add AjoutIndexCommandesStatutDate --project src/Textinord.Trame.Infrastructure` ; contenu attendu :

```csharp
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Textinord.Trame.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AjoutIndexCommandesStatutDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Commandes_Statut_Date",
                table: "Commandes",
                columns: new[] { "Statut", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Commandes_Statut_Date",
                table: "Commandes");
        }
    }
}
```

Sur SQL Server, on peut couvrir la liste ADV sans retour à la table en ajoutant les colonnes lues : `b.HasIndex(c => new { c.Statut, c.Date }).IncludeProperties(c => new { c.Numero, c.ClientId })` (extension du provider SQL Server).

### Partie 3 — Preuve : avant / après

| Méthode | Avant | Après |
|---|---|---|
| `ListeCommandesValidees` | 1 + 2 × 900 = 1 801 requêtes ; 900 `Commande`, 900 `Client`, 3 000 `LigneCommande` suivies | 1 requête : `SELECT c.Numero, c0.RaisonSociale, (SELECT COUNT(*) ...), (SELECT SUM(...) ...) FROM Commandes c INNER JOIN Clients c0 ... WHERE c.Statut = 'Validee' AND c.Date >= @p0 AND c.Date < @p1 ORDER BY c.Date` ; 0 entité suivie |
| `ExportCommandesFacturees` | 1 requête avec deux jointures (client répété sur chaque ligne) ; 4 200 entités suivies ; 140 Mo | 1 requête plate `SELECT c.Numero, c0.RaisonSociale, l.ReferenceArticle, l.Quantite, l.PrixUnitaire, l.RemisePourcent FROM Commandes c INNER JOIN Clients c0 ... INNER JOIN LignesCommande l ... ORDER BY c.Numero` ; 0 entité suivie ; 3 000 records légers |
| `SuiviClient` | 1 requête, 3 tables jointes (`Commandes`, `Clients`, `LignesCommande`), 20 commandes + 20 clients + ~60 lignes suivies | 1 requête, 1 table, 3 colonnes : `SELECT c.Numero, c.Date, c.Statut FROM Commandes c WHERE c.ClientId = @p0 ORDER BY c.Date DESC LIMIT 20` ; 0 entité suivie |
| Plan sur `Commandes` | scan de table (clustered index scan) puis tri | recherche sur `IX_Commandes_Statut_Date` (index seek), tri fourni par l'index |

Pour obtenir ces SQL sur SQLite en mémoire : `options.UseSqlite(connexion).LogTo(Console.WriteLine, [DbLoggerCategory.Database.Command.Name], LogLevel.Information)` ; ou `query.ToQueryString()` sans exécuter.

## Variantes acceptables

1. `ListeCommandesValidees` avec `Include(c => c.Client).Include(c => c.Lignes).AsNoTracking().AsSplitQuery()` puis calcul en mémoire : deux requêtes, zéro entité suivie ; acceptable, mais 4 800 objets matérialisés pour 900 lignes d'écran. Faire remarquer la différence de mémoire.
2. Index sur (`Statut`, `Date`) déclaré par attribut `[Index(nameof(Statut), nameof(Date))]` sur l'entité : fonctionne, mais met un attribut EF dans le domaine, ce que le TP 4 interdit. Accepter en signalant la fuite.
3. `SuiviClient` projetant directement dans le record avec `c.Statut.ToString()` : EF Core 10 traduit la conversion en chaîne pour une propriété mappée par `HasConversion<string>()` ; acceptable si le binôme a vérifié le SQL généré. En cas de doute, la projection anonyme puis la conversion en mémoire est la forme sûre.

## Erreurs classiques à repérer en correction

| Erreur observée | Cause probable | Comment corriger |
|---|---|---|
| `AsNoTracking()` ajouté partout, y compris sur `ListeCommandesValidees` avec la boucle conservée | on traite le symptôme mémoire, pas le N+1 | compter les requêtes avec `LogTo` : `AsNoTracking()` n'en retire aucune |
| Projection qui appelle `l.MontantHt` | propriété calculée en C#, ignorée par EF, non traduisible | écrire le calcul en LINQ (`Quantite * PrixUnitaire * (1 - RemisePourcent / 100m)`) |
| Index créé sur (`Date`, `Statut`) | ordre des colonnes inversé | la colonne d'égalité en premier ; montrer le plan : avec `Date` en tête, le filtre sur `Statut` parcourt tout l'index |
| Index ajouté en SQL à la main sur la base | la migration ne le connaît pas : il disparaîtra sur le prochain environnement | déclarer dans la configuration fluent, générer la migration |
| Chaîne de connexion déplacée dans `appsettings.json` avec le mot de passe | on a déplacé le secret, pas supprimé | Entra ID (`Authentication=Active Directory Default`) ou Key Vault ; en dev, `dotnet user-secrets` |

## Points à insister en débriefing

- Trois questions avant d'écrire une requête EF Core : est-ce que j'écris (sinon, projeter) ; combien de requêtes partent (lire le SQL) ; quel index la sert (regarder le plan).
- Le lazy loading n'est pas une optimisation : c'est une dette différée. Trame 2 ne l'active pas.
- La configuration du `DbContext` est de la configuration d'application : elle vit dans l'hôte (module 5), pas dans la classe.

## Bonus

Requête compilée pour `SuiviClient` :

```csharp
private static readonly Func<TrameDbContext, int, IAsyncEnumerable<LigneSuiviClient>> SuiviClientCompile =
    EF.CompileAsyncQuery((TrameDbContext db, int clientId) =>
        db.Commandes
            .Where(c => c.ClientId == clientId)
            .OrderByDescending(c => c.Date)
            .Take(20)
            .Select(c => new LigneSuiviClient(c.Numero, c.Date, c.Statut.ToString())));
```

Intercepteur de commandes lentes :

```csharp
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

public sealed class CommandesLentesInterceptor(ILogger<CommandesLentesInterceptor> logger, TimeSpan seuil)
    : DbCommandInterceptor
{
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (eventData.Duration > seuil)
        {
            logger.LogWarning("Commande lente ({Duree} ms) : {Sql}", eventData.Duration.TotalMilliseconds, command.CommandText);
        }
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }
}
```

Enregistrement : `options.AddInterceptors(new CommandesLentesInterceptor(logger, TimeSpan.FromMilliseconds(200)))`.
