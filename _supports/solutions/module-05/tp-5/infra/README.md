# Infrastructure — API Trame 2 sur Azure Container Apps

Ce dossier contient le déploiement de l'API sur Azure et, surtout, le **plan B local**
utilisé en formation (pas d'abonnement Azure, pas de Docker sur les postes).

## Ce que fait `deploy-aca.sh`

| Étape | Commande | Ressource créée |
|---|---|---|
| 1 | `az group create` | groupe de ressources `rg-textinord-trame2-dev` (France Centre) |
| 2 | `az acr create` | registre d'images `acrtextinordtrame2dev` (SKU Basic) |
| 3 | `dotnet publish /t:PublishContainer -p ContainerRegistry=…` | image `textinord/trame-api:1.0.0` construite et poussée sans Docker |
| 4 | `az containerapp env create` | environnement Container Apps + Log Analytics |
| 5-6 | `az containerapp create` | application, ingress externe 8080, 1 à 3 réplicas, identité managée + rôle AcrPull |
| 7 | `az containerapp update --yaml` | sondes `/health/live` et `/health/ready` |
| 8 | `curl` | vérification de `/health/ready` et du référentiel public |

Coût indicatif du groupe complet en dev : quelques euros par jour (Container Apps facture à la
consommation, l'ACR Basic environ 5 € par mois). `./infra/deploy-aca.sh --teardown` supprime tout.

## Plan B local (sans Azure, sans Docker)

1. **Lancer l'API seule**

   ```bash
   cd src/Textinord.Trame.Api
   dotnet user-jwts create --name sofia.marques --role adv
   dotnet run
   ```

   Ouvrir <http://localhost:5043/scalar>, coller le jeton dans le champ Bearer, tester
   `GET /api/v1/commandes`. Les traces OpenTelemetry s'affichent dans la console
   (`Telemetrie:Console = true` en développement).

2. **Lancer l'orchestrateur Aspire** (dashboard : traces, logs, métriques)

   ```bash
   dotnet run --project src/Textinord.Trame.AppHost
   ```

   Le dashboard s'ouvre sur `https://localhost:17043`. L'API y apparaît comme ressource
   `trame-api` avec ses endpoints, ses logs structurés et ses traces (dont les spans
   `commande.creer` et `commande.valider`). Aspire 13 ne demande aucun workload ;
   il télécharge son orchestrateur (DCP) et le dashboard depuis NuGet à la première restauration.

3. **Produire l'image sans démon Docker**

   ```bash
   dotnet publish src/Textinord.Trame.Api -c Release /t:PublishContainer \
     -p ContainerArchiveOutputPath=./out/trame-api.tar.gz
   ```

   Le SDK écrit une archive OCI chargeable ensuite avec `docker load`, `podman load`
   ou `nerdctl load` sur une machine qui dispose d'un moteur de containers.

4. **Avec Docker ou Podman disponibles**

   ```bash
   docker build -t textinord/trame-api:1.0.0 .
   docker run --rm -p 8080:8080 -e ASPNETCORE_ENVIRONMENT=Development textinord/trame-api:1.0.0
   curl http://localhost:8080/health/ready
   ```

   Podman : remplacer `docker` par `podman` (même Dockerfile, même image). Sans Dockerfile :
   `dotnet publish /t:PublishContainer` puis `docker run textinord/trame-api:1.0.0`.

## Ce qui change en production (à traiter aux modules 4 et 6)

- **État hors du processus** : le stockage en mémoire de ce TP ne survit ni au redémarrage ni à
  un second réplica. Azure SQL (EF Core) remplace `CommandesEnMemoire`.
- **Identité** : `AddMicrosoftIdentityWebApi(builder.Configuration)` et une section `AzureAd`
  remplacent la section `Authentication:Schemes:Bearer` de `dotnet user-jwts`.
- **Secrets** : Key Vault référencé par l'application container (`--secrets` avec référence
  Key Vault), jamais dans l'image ni dans le YAML.
- **Télémétrie** : l'exporter OTLP pointe vers Azure Monitor (Application Insights) via
  `Azure.Monitor.OpenTelemetry.AspNetCore`, ou vers un collecteur OpenTelemetry.
- **Pipeline** : les étapes 3 et 6 du script deviennent des tâches Azure Pipelines (module 6),
  avec une connexion de service fédérée à la place de `az login`.
