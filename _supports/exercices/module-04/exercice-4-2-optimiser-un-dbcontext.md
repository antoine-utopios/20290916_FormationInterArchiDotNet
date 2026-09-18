# Exercice 4.2 — Optimiser un DbContext et trois requêtes de Trame 2

> Module : 4 — Persistance : SQL, NoSQL, ADO.NET et EF Core
> Durée estimée : 25 min
> Difficulté : 3 / 5
> Type : Exercice de diagnostic et de correction de code, en binôme

## Objectifs pédagogiques

À la fin de cet exercice, vous serez capable de :

- Reconnaître un problème N+1 à la lecture du code et à la lecture du SQL généré
- Distinguer une requête qui a besoin du suivi (tracking) d'une requête qui n'en a pas besoin
- Remplacer un chargement d'agrégat complet par une projection quand l'écran ne lit que quelques colonnes
- Ajouter l'index qui manque à une requête filtrée et triée, et le porter dans une migration
- Repérer dans la configuration d'un `DbContext` les options qui coûtent en production

## Prérequis

- Avoir suivi la section 5 du module 4 (slides 21 à 28) et la démo 4.1
- SDK .NET 10 installé ; la solution du TP 2 ou une solution vide avec les paquets `Microsoft.EntityFrameworkCore.Sqlite` 10.0.x
- Le modèle du module (entités `Commande`, `LigneCommande`, `Client`, `Article`) : celui du TP 4, ou les classes fournies en annexe du TP 4

## Contexte

Sofia Marques a mesuré trois écrans de Trame 2 sur une base de test représentative (900 commandes du jour, 3 000 lignes, 1 200 clients, 40 000 articles), avec `LogTo` activé. Les chiffres qu'elle a relevés :

| Écran | Code | Symptôme mesuré |
|---|---|---|
| Liste ADV « commandes validées du jour » | `ListeCommandesValidees` | 901 requêtes SQL pour afficher 900 lignes ; 1,9 s |
| Export CSV comptable | `ExportCommandesFacturees` | 1 requête, mais 2,4 s et 140 Mo alloués ; le `DbContext` garde 4 200 entités en mémoire |
| Écran « suivi client » | `SuiviClient` | joint 3 tables et matérialise trois types d'entités pour afficher trois colonnes ; 350 ms par client |
| Toutes | plan d'exécution | `SELECT` sur `Commandes` filtré par `Statut` et trié par `Date` : scan complet de la table (analyse de plan dans SSMS) |

Elle vous confie le `DbContext` et les trois méthodes. Votre mission : trouver les quatre défauts annoncés (un N+1, un suivi inutile, une projection manquante, un index absent), les corriger, et prouver la correction par le SQL généré.

## Code fourni

### `TrameDbContext.cs` (version reçue)

```csharp
using Microsoft.EntityFrameworkCore;
using Textinord.Trame.Domain;

namespace Textinord.Trame.Infrastructure.Persistence;

public sealed class TrameDbContext : DbContext
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Commande> Commandes => Set<Commande>();
    public DbSet<LigneCommande> LignesCommande => Set<LigneCommande>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options
            .UseSqlServer("Server=sql-trame.textinord.local;Database=Trame2;User Id=trame;Password=Trame2026!;TrustServerCertificate=True")
            .UseLazyLoadingProxies()
            .EnableSensitiveDataLogging()
            .LogTo(Console.WriteLine);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Commande>(b =>
        {
            b.ToTable("Commandes");
            b.HasKey(c => c.Id);
            b.Property(c => c.Numero).HasMaxLength(15).IsRequired();
            b.Property(c => c.Statut).HasConversion<string>().HasMaxLength(20);
            b.HasOne(c => c.Client).WithMany().HasForeignKey(c => c.ClientId);
            b.HasMany(c => c.Lignes).WithOne().HasForeignKey("CommandeId").IsRequired();
            b.Navigation(c => c.Lignes).HasField("_lignes");
            b.OwnsOne(c => c.Metadonnees, m => m.ToJson());
            b.Property(c => c.Version).IsConcurrencyToken();
            b.Ignore(c => c.TotalHt);
        });

        modelBuilder.Entity<LigneCommande>(b =>
        {
            b.ToTable("LignesCommande");
            b.HasKey(l => l.Id);
            b.Property(l => l.PrixUnitaire).HasPrecision(18, 2);
            b.Property(l => l.RemisePourcent).HasPrecision(5, 2);
            b.HasOne<Article>().WithMany().HasForeignKey(l => l.ArticleId);
            b.Ignore(l => l.MontantHt);
        });

        modelBuilder.Entity<Client>(b =>
        {
            b.ToTable("Clients");
            b.HasKey(c => c.Id);
            b.Property(c => c.Code).HasMaxLength(20).IsRequired();
            b.Property(c => c.RaisonSociale).HasMaxLength(200).IsRequired();
            b.Property(c => c.ConditionTarifaire).HasConversion<string>().HasMaxLength(30);
        });

        modelBuilder.Entity<Article>(b =>
        {
            b.ToTable("Articles");
            b.HasKey(a => a.Id);
            b.Property(a => a.Reference).HasMaxLength(30).IsRequired();
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

### `RequetesCommandes.cs` (version reçue)

```csharp
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Textinord.Trame.Domain;

namespace Textinord.Trame.Infrastructure.Lecture;

public sealed record LigneListeAdv(string Numero, string Client, int NombreLignes, decimal TotalHt);

public sealed record LigneSuiviClient(string Numero, DateTime Date, string Statut);

public sealed class RequetesCommandes(TrameDbContext db)
{
    // Écran ADV : commandes validées du jour, avec le nom du client et le total
    public async Task<List<LigneListeAdv>> ListeCommandesValidees(DateTime jour, CancellationToken ct)
    {
        var commandes = await db.Commandes
            .Where(c => c.Statut == StatutCommande.Validee && c.Date >= jour && c.Date < jour.AddDays(1))
            .OrderBy(c => c.Date)
            .ToListAsync(ct);

        var resultat = new List<LigneListeAdv>();
        foreach (var commande in commandes)
        {
            var client = await db.Clients.SingleAsync(cl => cl.Id == commande.ClientId, ct);
            var lignes = await db.LignesCommande
                .Where(l => EF.Property<int>(l, "CommandeId") == commande.Id)
                .ToListAsync(ct);
            resultat.Add(new LigneListeAdv(commande.Numero, client.RaisonSociale, lignes.Count,
                lignes.Sum(l => l.MontantHt)));
        }
        return resultat;
    }

    // Export comptable : toutes les commandes facturées du mois, une ligne CSV par ligne de commande
    public async Task<string> ExportCommandesFacturees(int annee, int mois, CancellationToken ct)
    {
        var debut = new DateTime(annee, mois, 1, 0, 0, 0, DateTimeKind.Utc);
        var fin = debut.AddMonths(1);

        var commandes = await db.Commandes
            .Include(c => c.Client)
            .Include(c => c.Lignes)
            .Where(c => c.Statut == StatutCommande.Facturee && c.Date >= debut && c.Date < fin)
            .OrderBy(c => c.Numero)
            .ToListAsync(ct);

        var csv = new StringBuilder("Numero;Client;Article;Quantite;PrixUnitaire;Remise;MontantHt\n");
        foreach (var c in commandes)
        {
            foreach (var l in c.Lignes)
            {
                csv.Append(CultureInfo.InvariantCulture,
                    $"{c.Numero};{c.Client!.RaisonSociale};{l.ReferenceArticle};{l.Quantite};{l.PrixUnitaire};{l.RemisePourcent};{l.MontantHt}\n");
            }
        }
        return csv.ToString();
    }

    // Écran « suivi client » : les 20 dernières commandes d'un client, numéro, date et statut
    public async Task<List<LigneSuiviClient>> SuiviClient(int clientId, CancellationToken ct)
    {
        var commandes = await db.Commandes
            .Include(c => c.Client)
            .Include(c => c.Lignes)
            .Where(c => c.ClientId == clientId)
            .OrderByDescending(c => c.Date)
            .Take(20)
            .ToListAsync(ct);

        return commandes
            .Select(c => new LigneSuiviClient(c.Numero, c.Date, c.Statut.ToString()))
            .ToList();
    }
}
```

## Énoncé

### Partie 1 — Diagnostic (8 min)

Sans exécuter le code, annotez chaque méthode et le `DbContext` :

1. Quelle méthode produit le N+1 ? Combien de requêtes pour 900 commandes ? Où exactement dans le code ?
2. Quelle méthode suit des entités dont elle n'a pas besoin ? Que coûte ce suivi (mémoire, temps) ?
3. Quelle méthode charge des données qu'elle n'affiche pas ? Quelles tables sont jointes pour rien ?
4. Quelle requête réclame un index composé ? Sur quelles colonnes, dans quel ordre ?
5. Dans `OnConfiguring`, relevez trois choix qui ne doivent pas atteindre la production et dites par quoi les remplacer.

### Partie 2 — Correction (12 min)

Réécrivez les trois méthodes et la configuration :

- `ListeCommandesValidees` : une seule requête SQL, sans charger les entités `Commande` ni `LigneCommande` ;
- `ExportCommandesFacturees` : aucune entité suivie à la fin de la méthode (`db.ChangeTracker.Entries().Count() == 0`) et, si vous gardez les `Include`, une stratégie contre l'explosion cartésienne ;
- `SuiviClient` : une projection qui ne lit que les trois colonnes affichées ;
- l'index manquant, déclaré dans la configuration fluent, avec la migration correspondante (`dotnet ef migrations add AjoutIndexCommandesStatutDate`) ;
- `OnConfiguring` remplacé par une configuration injectée (`DbContextOptions<TrameDbContext>`), la chaîne de connexion sortie du code, le lazy loading retiré.

### Partie 3 — Preuve (5 min)

Pour chacune des trois méthodes, capturez le SQL généré avant et après (via `LogTo`, sur SQLite en mémoire ou sur votre base) et notez : nombre de requêtes, tables jointes, colonnes sélectionnées. Présentez le résultat sous la forme d'un tableau avant / après.

Résultat attendu : `TrameDbContext.cs` et `RequetesCommandes.cs` corrigés, une migration ajoutant l'index, et un tableau avant / après de trois lignes.

## Indices (à consulter si bloqué)

<details>
<summary>Indice 1 — Compter les requêtes</summary>

Comptez les `await` qui se trouvent à l'intérieur d'un `foreach`. Chaque `await` dans une boucle est une requête par itération. Deux `await` dans une boucle de 900 tours, c'est 1 800 requêtes de plus que nécessaire, plus la requête initiale.

</details>

<details>
<summary>Indice 2 — Le suivi</summary>

Une méthode qui retourne un `string` ou un DTO n'a pas besoin que le contexte se souvienne des entités. Cherchez `AsNoTracking()`, ou mieux, une projection `Select` qui ne matérialise jamais d'entité. Pour les `Include` d'une collection, regardez `AsSplitQuery()`.

</details>

<details>
<summary>Indice 3 — L'index</summary>

Regardez la clause `WHERE` et la clause `ORDER BY` de la requête la plus fréquente. Un index composé se lit de gauche à droite : la colonne d'égalité en premier, la colonne de tri ensuite. Vérifiez qu'un index sur `Statut` seul ne suffit pas.

</details>

<details>
<summary>Indice 4 — Le calcul du total en projection</summary>

`MontantHt` est une propriété calculée en C# (ignorée par EF). Une projection doit exprimer le calcul en LINQ traduisible : `c.Lignes.Sum(l => l.Quantite * l.PrixUnitaire * (1 - l.RemisePourcent / 100m))`. Sur SQLite, `Sum` sur `decimal` n'est pas traduit : faites le calcul sur `double` dans la projection ou testez sur SQL Server.

</details>

## Pour aller plus loin (bonus)

1. Écrivez une requête compilée (`EF.CompileAsyncQuery`) pour `SuiviClient`, la plus appelée des trois, et mesurez la différence sur 1 000 appels.
2. Ajoutez un intercepteur `DbCommandInterceptor` qui journalise en avertissement toute commande dépassant 200 ms, avec son SQL : c'est le détecteur de régression le moins cher qui existe.
