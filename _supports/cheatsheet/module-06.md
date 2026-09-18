# Cheatsheet — Module 6 : Legacy, messaging, identité d'entreprise et industrialisation

Condensé apprenant. Positionnement septembre 2026 : .NET 10, C# 14, ASP.NET Core 10, EF Core 10, MassTransit 8, Azure DevOps et GitHub Actions.

## 1. Stratégies de modernisation

| Stratégie | En une phrase | Quand | Exemple Trame |
|---|---|---|---|
| Rehost | même code, autre hébergement | gagner du temps, dette intacte | serveur IIS de Trame en VM Azure pendant la transition |
| Replatform | même code, plateforme modernisée (.NET Framework 4.8 → .NET 10) | code sain mais plateforme en fin de vie | postes WinForms ADV sur .NET 10 |
| Refactor | même fonctionnel, architecture reprise | dette structurelle, besoin de tests et d'API | commandes, ordres de préparation, tarifs |
| Rebuild | réécriture | technologie sans chemin de migration | site Web Forms → Blazor |

Outillage : `.NET Upgrade Assistant` (`dotnet upgrade`), analyseurs de compatibilité (`Microsoft.DotNet.Analyzers.Compatibility`, `ApiPort`), `.NET Standard 2.0` comme pont, `Microsoft.AspNetCore.SystemWebAdapters` (session et authentification partagées), Windows Compatibility Pack.

**Strangler Fig avec YARP** : un reverse proxy ASP.NET Core devant Trame ; chaque fonctionnalité migrée = une route de plus vers Trame 2 ; retour arrière = changer le `ClusterId` de la route ; la route `{**rest}` vers le legacy reste en dernier. **Anti-Corruption Layer** : un adaptateur, côté Trame 2, qui traduit les formats de Trame (DataSet WCF, procédures stockées, fichiers EDI, base comptable) vers les entités du domaine ; c'est le code qui disparaît à la fin.

Ordre de bascule : valeur métier, risque technique, dépendances ; commencer par ce qui se lit (catalogue), finir par ce qui engage l'argent (facturation).

## 2. Messaging : RabbitMQ, ActiveMQ Artemis, Azure Service Bus

| | RabbitMQ | ActiveMQ Artemis | Azure Service Bus |
|---|---|---|---|
| Protocole | AMQP 0-9-1 (+ MQTT, STOMP, AMQP 1.0) | AMQP 1.0, OpenWire, MQTT, STOMP, JMS | AMQP 1.0, HTTPS |
| Modèle | exchange (direct, topic, fanout, headers) → bindings → files | adresses, files anycast / multicast | files ; topics + abonnements avec filtres SQL |
| Fiabilité | files durables + messages persistants + confirmations éditeur ; `ack` / `nack` ; dead letter exchange ; quorum queues ; prefetch | journal persistant, redélivrance, DLQ, transactions JMS | peek-lock (`Complete` / `Abandon`), `$DeadLetterQueue`, sessions (ordre par clé), déduplication sur `MessageId`, envoi planifié, TTL, géo-réplication |
| Hébergement | vous (Windows, Linux, Docker) ou managé | vous (Java), Red Hat AMQ | PaaS Azure (Basic, Standard, Premium) |
| .NET | `RabbitMQ.Client`, MassTransit, Wolverine, NServiceBus | `Apache.NMS.AMQP`, MassTransit | `Azure.Messaging.ServiceBus`, MassTransit, NServiceBus, Azure Functions |
| Textinord | développement local, entrepôts autonomes | non retenu | production : topics `commandes`, `preparations` |

**MSMQ** : Windows uniquement, absent de .NET, files locales non transactionnelles → on remplace le canal derrière une abstraction (MassTransit), le transport devient de la configuration ; un message bridge relit MSMQ pendant la transition.

**Abstractions** : MassTransit 8 (Apache 2.0 ; v9 commerciale) — `AddMassTransit(x => { x.AddConsumer<T>(); x.UsingInMemory(...) | UsingRabbitMq(...) | UsingAzureServiceBus(...); })`, `UseMessageRetry`, `AddEntityFrameworkOutbox`, sagas, test harness ; Wolverine (handlers par convention, Outbox intégrée) ; NServiceBus (commercial).

Un consumer :

```csharp
public sealed class OrdrePreparationConsumer(TrameDbContext db) : IConsumer<CommandeValidee>
{
    public async Task Consume(ConsumeContext<CommandeValidee> ctx)
    {
        if (await db.OrdresPreparation.AnyAsync(o => o.CommandeId == ctx.Message.CommandeId)) return;
        // ... créer un ordre par entrepôt, SaveChangesAsync
    }
}
```

## 3. Outbox en cinq lignes

1. Le producteur écrit la donnée métier **et** le message (table `Outbox` : `MessageId`, `Type`, `Contenu` JSON, `EnvoyeLe`, `Tentatives`) dans **une seule transaction** — un seul `SaveChangesAsync`.
2. Un relais (`BackgroundService`) lit les messages non envoyés, les publie sur le bus avec le même `MessageId`, puis marque `EnvoyeLe`.
3. Garantie **au moins une fois** : si le relais tombe entre publication et marquage, le message est republié.
4. Le consommateur est donc **idempotent** : clé naturelle unique (commande + entrepôt) ou table Inbox des `MessageId` traités.
5. Retry borné (1 s, 5 s, 30 s) pour le transitoire ; au-delà, dead letter ou file `_error`, alerte, rejeu humain. Jamais de retry infini.

## 4. Transactions : `TransactionScope` local ou saga

| Besoin | Réponse .NET 10 | À proscrire |
|---|---|---|
| Plusieurs écritures dans une base | un `SaveChangesAsync` (une transaction implicite) ; `BeginTransactionAsync` si plusieurs `SaveChanges` ou Dapper sur la même connexion | — |
| `TransactionScope` | reste **local** : une seule connexion ; la promotion vers MSDTC est désactivée par défaut (Windows seulement, sur demande) ; deux connexions = exception | MTS, COM+ (`ServicedComponent`), DTC : 2PC, verrous tenus, blocage si le coordinateur tombe |
| Une base + un broker | Outbox | `TransactionScope` englobant SQL Server + MSMQ |
| Plusieurs systèmes (stock, entrepôts, facturation) | **saga** : chorégraphie par événements ou orchestration par machine à états (`MassTransitStateMachine<T>`) ; **compensation** explicite (`AnnulerReservationStock`) ; timeouts ; cohérence à terme acceptée par le métier | transaction inter-bases |

Règle Trame 2 : une transaction = une base. Tout ce qui traverse une frontière passe par un message et une compensation.

## 5. LDAP, Active Directory, Entra ID, fédération

- **LDAP depuis .NET 10** : `System.DirectoryServices.Protocols` (multiplateforme) — `LdapConnection`, `AuthType.Negotiate`, `SecureSocketLayer = true` (636), `SearchRequest(base, filtre, SearchScope.Subtree, attributs)` ; `System.DirectoryServices.AccountManagement` (`PrincipalContext`) = Windows uniquement ; `Novell.Directory.Ldap.NETStandard` = alternative (OpenLDAP compris).
- **Kerberos / NTLM dans ASP.NET Core** : `AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate()` pour l'intranet ; les groupes AD deviennent des claims de rôle.
- **Hybride** : AD DS on-premise (source des comptes) + **Entra Connect** (synchronisation, hash de mot de passe ou SSO transparent) + **Microsoft Entra ID** (OIDC / OAuth 2.0, MFA, accès conditionnel, app registrations) ; **Entra Domain Services** = AD managé dans Azure pour le legacy ; **Entra External ID** = clients et partenaires ; **Microsoft Graph** = API (`/me/memberOf`).
- **Groupes → rôles** : claims `groups` dans le jeton, une policy ASP.NET Core par rôle ; pas d'appel LDAP par requête.
- **Fédération** : WS-Federation (WIF, ADFS) = legacy, `Microsoft.AspNetCore.Authentication.WsFederation` sert à migrer ; SAML 2.0 = partenaires avec leur IdP (Entra en IdP ou SP) ; OpenID Connect / OAuth 2.0 = la cible (`Microsoft.Identity.Web`) ; B2B = l'invité se connecte avec son propre compte ; une app registration par application, un service principal par environnement.

## 6. Pyramide des tests et outils

| Niveau | Part | Outils | Ce qu'on vérifie | Durée |
|---|---|---|---|---|
| Unitaires | 70 % | xUnit (`[Fact]`, `[Theory]` + `[InlineData]`, `IClassFixture<T>`, `IAsyncLifetime`), NSubstitute (`Substitute.For<T>()`, `Returns`, `ThrowsAsync`, `Received(n)`, `DidNotReceiveWithAnyArgs`, `Received.InOrder`), Bogus (`Faker<T>`, `UseSeed`), `TimeProvider` substitué | une règle, une classe : remise plafonnée, statut, numéro | ms |
| Intégration | 20 % | `WebApplicationFactory<Program>` (+ `public partial class Program;`), SQLite en mémoire (`Data Source=:memory:` sur une connexion ouverte) ou fichier temporaire, MassTransit test harness (`AddMassTransitTestHarness`, `harness.Consumed.Any<T>()`), Testcontainers si Docker | API + EF Core + Outbox ensemble ; relais → consumer → base | s |
| Validation | 10 % | Playwright (`Microsoft.Playwright`), Reqnroll (Gherkin, ex SpecFlow) | parcours complet ; acceptation métier | min |
| Exploratoire | — | Azure Test Plans | ce qu'aucun script ne prévoit | h |

Règle : un bug corrigé = un test unitaire ajouté ; un incident de production = un test d'intégration ajouté. Un test doit passer au rouge quand on retire la règle qu'il protège.

## 7. SemVer et gestion des versions

- `MAJEUR.MINEUR.CORRECTIF` : rupture de contrat / ajout compatible / correction ; pré-version `2.1.0-beta.12` ; métadonnées `+g3f2a1c`.
- **Nerdbank.GitVersioning** : `version.json` (`"version": "2.1-beta"`, `publicReleaseRefSpec` pour `main` et `release/vX.Y`) ; le correctif = hauteur Git ; `nbgv prepare-release` crée `release/v2.1` et passe `main` en `2.2-beta` ; `nbgv cloud` renseigne le numéro de build ; `fetchDepth: 0` obligatoire en CI. Alternative : GitVersion.
- **Branches** : trunk-based (branches courtes, feature flags, `main` toujours livrable) pour Trame 2 ; GitFlow (`develop`, `release/*`, `hotfix/*`) quand plusieurs versions vivent en parallèle ; support N-1 par branche `release/vN-1` et cherry-pick vers `main`.
- **Feature flags** : `Microsoft.FeatureManagement`, Azure App Configuration — livrer inactif, activer par entrepôt.
- Versionner aussi les contrats : API v1 / v2, messages (`CommandeValidee` v2 = nouveau type).
- **CHANGELOG.md** (Keep a Changelog) : Non publié / versions datées ; Ajouté, Modifié, Corrigé, Retiré, Rupture, Technique ; un identifiant de work item par ligne.

## 8. Anatomie d'un pipeline YAML

**Azure DevOps** (`azure-pipelines.yml`) :

```yaml
trigger: { branches: { include: [main, release/*] } }
pr:      { branches: { include: [main] } }
variables: [ { group: trame2-secrets }, { name: buildConfiguration, value: Release } ]
pool: { vmImage: ubuntu-latest }
stages:
  - stage: Build            # checkout fetchDepth 0, UseDotNet@2, Cache@2, nbgv cloud, restore, build
  - stage: Test             # DotNetCoreCLI@2 test --collect:"XPlat Code Coverage" --logger trx
                            # publishTestResults: true, PublishCodeCoverageResults@2
  - stage: Publish          # condition: ne(variables['Build.Reason'], 'PullRequest')
                            # dotnet publish, PublishPipelineArtifact@1, dotnet publish -t:PublishContainer
  - stage: Deploy_Recette   # deployment job, environment: trame2-recette, AzureContainerApps@1, test de fumée
  - stage: Deploy_Production
    condition: startsWith(variables['Build.SourceBranch'], 'refs/heads/release/')
    jobs:
      - deployment: Production
        environment: trame2-production      # approbations et checks configurés SUR L'ENVIRONNEMENT
        strategy:
          runOnce: { deploy: ..., routeTraffic: ..., postRouteTraffic: ..., on: { failure: ... } }
```

Vocabulaire : `stage` → `job` / `deployment` → `steps` (`task`, `script`) ; `dependsOn`, `condition` ; `environment` (traçabilité, approbations, checks) ; service connections et groupes de variables pour les secrets ; agents Microsoft-hosted ou self-hosted.

**GitHub Actions** (`.github/workflows/ci.yml`) : `on: push / pull_request` → `jobs:` (`build-test`, `publish`, `deploy-recette`, `deploy-production`) avec `needs:`, `if:`, `environment: { name, url }` (reviewers requis dans Settings > Environments), `permissions:` (`id-token: write` pour Azure par OIDC), `actions/checkout@v4` (`fetch-depth: 0`), `actions/setup-dotnet@v4`, `dotnet/nbgv@v0.4`, `actions/upload-artifact@v4`, `azure/login@v2`, rollback par `if: failure()`.

**Qualité en continu** : `TreatWarningsAsErrors`, analyzers Roslyn, SonarQube / SonarCloud (quality gate), couverture Cobertura publiée, audit NuGet (`NU1903` : dépendance vulnérable = build cassé, épingler une version corrigée dans `Directory.Packages.props`), SBOM CycloneDX, images signées.

**Releases** : mêmes artefacts promus d'un environnement à l'autre (développement → intégration → recette → production) ; approbations ; blue / green (bascule complète, retour immédiat) ; canary (10 % du trafic, observation, 100 %) ; rollback = redéployer l'artefact précédent ; schéma compatible N-1.

## Azure DevOps en un tableau

| Service | Rôle | Équivalent GitHub |
|---|---|---|
| Boards | epics, features, PBI, bugs, sprints, Kanban | Issues, Projects |
| Repos | Git, pull requests, branch policies | dépôt, PR, rulesets |
| Pipelines | YAML, environnements, approbations, templates | Actions, environments |
| Artifacts | flux NuGet privé, artefacts de pipeline | Packages |
| Test Plans | tests manuels et exploratoires, traçabilité | extensions tierces |
