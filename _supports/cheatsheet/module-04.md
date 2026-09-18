# Cheatsheet — Module 4 : Persistance (SQL, NoSQL, ADO.NET, EF Core 10)

Condensé apprenant. Positionnement septembre 2026 : .NET 10, C# 14, EF Core 10, `Microsoft.Data.SqlClient`, Dapper, Azure SQL, Cosmos DB, Redis.

## 1. Choisir le stockage : quatre questions, dans l'ordre

| Question | Si « oui » | Moteurs | Chez Textinord |
|---|---|---|---|
| Faut-il des transactions multi-tables et des jointures ? | Relationnel | Azure SQL, SQL Server 2025, PostgreSQL 17 | commandes, clients, stock (source de vérité), audit |
| Lit-on toujours l'objet entier, avec un schéma variable ? | Document | Azure Cosmos DB (API NoSQL), MongoDB | catalogue publié à l'extranet (copie) |
| Peut-on la perdre et la recalculer ? | Clé-valeur | Redis, Azure Cache for Redis | panier, sessions, cache de stock (TTL 30 s) |
| Volume ou relations tels que SQL ne suit plus ? | Colonnes larges, graphe | Cassandra, Cosmos DB (Gremlin) | hors périmètre |

Règles : SQL par défaut ; un seul moteur « source de vérité » par donnée ; chaque choix est une ADR avec sa date de revue.

CAP : en cas de partition réseau, cohérence ou disponibilité. PACELC : hors panne, latence ou cohérence. Un SGBD SQL sur un nœud n'est pas concerné par CAP. Cosmos DB : cinq niveaux de cohérence (Strong, Bounded Staleness, Session, Consistent Prefix, Eventual).

## 2. Ce qu'un SGBD SQL sait faire

| Sujet | À retenir |
|---|---|
| ACID | atomicité, cohérence, isolation, durabilité ; l'isolation est réglable |
| Isolation | Read Uncommitted (jamais) ; Read Committed (défaut SQL Server) ; **RCSI** (recommandé, défaut Azure SQL) ; Repeatable Read ; Snapshot ; Serializable (numérotation) |
| Index | clustered (un par table), non-clustered + colonnes incluses, filtré, columnstore ; composé : colonne d'égalité puis colonne de tri |
| Plan | plan réel (SSMS, Azure Data Studio), `SET STATISTICS IO`, Query Store |
| Contraintes | PK, unicité, FK, `CHECK` : la base garantit l'invariant |
| Verrous | ligne / page / table, escalade ; deadlock = victime choisie ; parades : transactions courtes, même ordre d'accès, RCSI |
| Concurrence | `rowversion` (SQL Server), colonne gérée par l'application (portable) |
| Historique | temporal tables (`SYSTEM_VERSIONING = ON`) |
| JSON | type `json` natif (SQL Server 2025), `JSON_VALUE`, index ; `jsonb` + GIN sur PostgreSQL |
| Sécurité | Row-Level Security, Always Encrypted, TDE, authentification Entra ID |

## 3. Patterns de persistance (Fowler, PoEAA) et leur équivalent EF Core

| Pattern | Idée | Avec EF Core 10 |
|---|---|---|
| Table Data Gateway | une classe par table, SQL dedans | Dapper + record de lecture |
| Row Data Gateway | un objet par ligne, sans logique | à éviter : c'est un DTO déguisé |
| Active Record | l'entité se sauvegarde elle-même | à éviter : couple domaine et base |
| Data Mapper | une couche traduit objets et tables | **EF Core lui-même** |
| Repository | collection d'agrégats, un par agrégat | `ICommandeRepository` dans le domaine, implémenté dans Infrastructure |
| Unit of Work | regroupe les écritures en une transaction | **le `DbContext`** (`SaveChangesAsync`) |
| Identity Map | une instance par clé | le change tracker |
| Lazy Load | charger à la demande | proxies : à éviter (N+1) ; explicit loading à la place |
| Query Object / Specification | critère réutilisable | `Expression<Func<T, bool>>` traduisible |
| Optimistic Offline Lock | version comparée à l'écriture | `IsConcurrencyToken()`, `IsRowVersion()`, `DbUpdateConcurrencyException` |
| Pessimistic Offline Lock | verrou applicatif explicite | rare ; `UPDLOCK` via SQL brut, ou table de verrous |
| Embedded Value | valeur sans identité dans la table | owned type, complex type |
| Metadata Mapping | où vit le mapping | conventions, attributs, fluent (`IEntityTypeConfiguration<T>`) |
| Héritage | TPH / TPT / TPC | `HasDiscriminator()`, `UseTptMappingStrategy()`, `UseTpcMappingStrategy()` |

Ne pas réécrire : `IRepository<T>` générique avec `GetAll()` qui expose `IQueryable`, `IUnitOfWork` par-dessus le `DbContext`.

## 4. ADO.NET et Dapper

```csharp
await using var cnx = new SqlConnection(chaine);          // pool automatique
await cnx.OpenAsync(ct);
await using var cmd = new SqlCommand("SELECT Numero FROM Commandes WHERE ClientId = @c", cnx);
cmd.Parameters.Add("@c", SqlDbType.Int).Value = clientId;  // toujours paramétré
await using var lecteur = await cmd.ExecuteReaderAsync(ct);
while (await lecteur.ReadAsync(ct)) { numeros.Add(lecteur.GetString(0)); }

// Dapper : SQL explicite, mapping par nom de colonne
var resumes = await cnx.QueryAsync<CommandeResume>(sql, new { Debut = debut, Fin = fin });
```

- `Microsoft.Data.SqlClient` (pas `System.Data.SqlClient`) ; ouvrir tard, fermer tôt ; `async` partout.
- Transactions : `BeginTransactionAsync` sur la connexion ; `TransactionScope(TransactionScopeAsyncFlowOption.Enabled)` ; jamais distribuée.
- Dapper : `QueryAsync<T>`, `QueryFirstOrDefaultAsync<T>`, `ExecuteAsync` ; partage la connexion et la transaction d'EF Core (`db.Database.GetDbConnection()`, `db.Database.CurrentTransaction`).

## 5. EF Core 10 : aide-mémoire

### Configuration

```csharp
services.AddDbContext<TrameDbContext>(o => o.UseSqlite(chaine));   // ou UseSqlServer(chaine, s => s.EnableRetryOnFailure())
services.AddDbContextPool<TrameDbContext>(...)                     // API sous charge

public sealed class CommandeConfiguration : IEntityTypeConfiguration<Commande>
{
    public void Configure(EntityTypeBuilder<Commande> b)
    {
        b.ToTable("Commandes"); b.HasKey(c => c.Id);
        b.Property(c => c.Numero).HasMaxLength(15).IsRequired(); b.HasIndex(c => c.Numero).IsUnique();
        b.Property(c => c.Statut).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(c => new { c.Statut, c.Date });
        b.HasOne(c => c.Client).WithMany().HasForeignKey(c => c.ClientId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(c => c.Lignes).WithOne().HasForeignKey("CommandeId").IsRequired().OnDelete(DeleteBehavior.Cascade);
        b.Navigation(c => c.Lignes).HasField("_lignes");
        b.OwnsOne(c => c.Metadonnees, m => m.ToJson());          // colonne JSON
        b.Property(c => c.Version).IsConcurrencyToken();          // ou b.Property<byte[]>("RowVersion").IsRowVersion() sur SQL Server
        b.Ignore(c => c.TotalHt);
    }
}
modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrameDbContext).Assembly);
```

### Lecture

| Besoin | Écrire |
|---|---|
| Écran, export, liste | `.Select(c => new Dto(...))` : pas d'entité, pas de suivi |
| Lecture seule d'entités | `.AsNoTracking()` ; `.AsNoTrackingWithIdentityResolution()` si des références partagées comptent |
| Agrégat à modifier | `.Include(c => c.Lignes).Include(c => c.Client).FirstAsync(...)` (suivi) |
| Collection + collection | `.AsSplitQuery()` contre l'explosion cartésienne |
| Chargement à la demande | `await db.Entry(c).Collection(x => x.Lignes).LoadAsync()` (explicit) ; pas de proxies |
| Filtrer sur une colonne JSON | `.Where(c => c.Metadonnees.Origine == "EDI")` (traduit en `JSON_VALUE` / `json_extract`) |
| Requête très fréquente | `EF.CompileAsyncQuery((TrameDbContext db, int id) => db.Commandes.First(c => c.Id == id))` |
| Voir le SQL | `options.LogTo(Console.WriteLine, LogLevel.Information)` ; `query.ToQueryString()` |

### Écriture

| Besoin | Écrire |
|---|---|
| Sauvegarder un cas d'usage | `await db.SaveChangesAsync(ct)` : une transaction, colonnes modifiées seulement |
| Transaction explicite | `await using var tx = await db.Database.BeginTransactionAsync(ct); ... await tx.CommitAsync(ct);` |
| Partager avec Dapper | `db.Database.GetDbConnection()`, `db.Database.CurrentTransaction?.GetDbTransaction()` |
| Mise à jour en masse | `await db.Commandes.Where(...).ExecuteUpdateAsync(s => s.SetProperty(c => c.Statut, StatutCommande.Facturee), ct)` |
| Suppression en masse | `await db.Commandes.Where(...).ExecuteDeleteAsync(ct)` |
| Conflit de concurrence | `catch (DbUpdateConcurrencyException ex) { await ex.Entries[0].ReloadAsync(); ... }` |
| Interception | `options.AddInterceptors(new AuditInterceptor())` (`SaveChangesInterceptor`, `DbCommandInterceptor`) |

### Concurrence optimiste, en une phrase

L'UPDATE généré contient `WHERE Id = @id AND Version = @ancienne` ; zéro ligne touchée = `DbUpdateConcurrencyException`. Résolution : recharger (la base a raison), fusionner, ou rendre la main à l'utilisateur.

## 6. Commandes `dotnet ef`

```bash
dotnet new tool-manifest                       # une fois par dépôt
dotnet tool install dotnet-ef                  # outil local, versionné avec le dépôt
dotnet dotnet-ef migrations add Initiale --project src/Textinord.Trame.Infrastructure --output-dir Persistence/Migrations
dotnet dotnet-ef migrations list --project src/Textinord.Trame.Infrastructure
dotnet dotnet-ef migrations remove --project src/Textinord.Trame.Infrastructure
dotnet dotnet-ef database update --project src/Textinord.Trame.Infrastructure
dotnet dotnet-ef migrations script --idempotent -o deploy/trame2.sql --project src/Textinord.Trame.Infrastructure
dotnet dotnet-ef dbcontext scaffold "<chaine>" Microsoft.EntityFrameworkCore.SqlServer -o Persistence/Legacy   # base existante
dotnet dotnet-ef dbcontext optimize --project src/Textinord.Trame.Infrastructure   # modèle compilé (démarrage)
```

Une classe `IDesignTimeDbContextFactory<TrameDbContext>` dans Infrastructure évite d'avoir besoin d'un projet de démarrage. En production : script idempotent joué par le pipeline, jamais `Migrate()` au démarrage d'une API répliquée.

## 7. NoSQL et cache en .NET

| Moteur | Paquet | Points d'attention |
|---|---|---|
| Azure Cosmos DB | `Microsoft.Azure.Cosmos` ou `Microsoft.EntityFrameworkCore.Cosmos` | clé de partition immuable ; coût en RU/s ; cohérence Session par défaut ; imbriquer ce qui est lu ensemble, référencer ce qui est partagé |
| MongoDB | `MongoDB.Driver` ou `MongoDB.EntityFrameworkCore` | mêmes principes de modélisation document |
| Redis | `Microsoft.Extensions.Caching.StackExchangeRedis`, `Microsoft.Extensions.Caching.Hybrid` | `IDistributedCache` (octets, TTL) ; `HybridCache` (L1 + L2, anti-stampede) ; invalider par événement |

## 8. Sérialiser en base

| Besoin | Solution | Vigilance |
|---|---|---|
| Attributs variables | colonne JSON (`ToJson()`), `jsonb` | champ `SchemaVersion`, lecture tolérante aux champs absents |
| Sérialisation .NET | `System.Text.Json`, options partagées, source generators | culture, `DateTimeOffset`, enums en chaîne (`JsonStringEnumConverter`) |
| Fichiers, images | Azure Blob Storage + chemin en base | pas de gros `varbinary(max)` dans une table transactionnelle |
| Contrats binaires | Protobuf (`Google.Protobuf`), MessagePack | champs numérotés, jamais renumérotés |
| `BinaryFormatter` | supprimé de .NET 9 | migrer les colonnes `image` de Trame vers JSON |

## 9. Legacy : Linq to SQL et EF6

Linq to SQL (2007, SQL Server seulement, `.dbml`) et EF6 (EDMX) ne reçoivent plus d'évolution. Chemin : entités POCO, `DbContext` EF Core avec fluent équivalent, `dotnet ef dbcontext scaffold` depuis la base existante, tests de non-régression sur les requêtes. Aucun nouveau code sur ces piles.
