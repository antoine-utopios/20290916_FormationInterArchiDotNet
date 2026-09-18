#!/usr/bin/env bash
# =============================================================================================
# Déploiement de l'API Trame 2 sur Azure Container Apps — Textinord, TP 5.
#
# Ce script n'est PAS exécuté en salle (pas d'abonnement Azure) : il se lit comme un déroulé,
# chaque commande est commentée. Prérequis pour l'exécuter chez Textinord :
#   - Azure CLI ≥ 2.70 avec l'extension containerapp : az extension add --name containerapp
#   - un abonnement où Nadia Benali (DSI) a délégué le rôle Contributeur sur le groupe de ressources
#   - le SDK .NET 10 (l'image est produite par `dotnet publish`, sans Docker)
#
# Usage : ./infra/deploy-aca.sh [--teardown]
# =============================================================================================
set -euo pipefail

# ----- Paramètres ---------------------------------------------------------------------------
LOCATION="francecentral"                       # région : données en France (RGPD, latence Roubaix)
RG="rg-textinord-trame2-dev"                   # groupe de ressources = unité de facturation et de suppression
ACR="acrtextinordtrame2dev"                    # registre d'images (nom global, alphanumérique uniquement)
ENV_NAME="cae-trame2-dev"                      # environnement Container Apps (réseau + logs partagés)
APP_NAME="ca-trame-api"                        # l'application container elle-même
LAW_NAME="law-trame2-dev"                      # Log Analytics : les logs et sondes y arrivent
IMAGE_NAME="textinord/trame-api"               # doit correspondre à <ContainerRepository> du csproj
IMAGE_TAG="1.0.0"                              # doit correspondre à <ContainerImageTags>
API_PROJECT="src/Textinord.Trame.Api/Textinord.Trame.Api.csproj"

# ----- Teardown -----------------------------------------------------------------------------
if [[ "${1:-}" == "--teardown" ]]; then
  echo "Suppression du groupe de ressources $RG (toutes les ressources qu'il contient)…"
  az group delete --name "$RG" --yes --no-wait
  exit 0
fi

# ----- 0. Connexion et abonnement -----------------------------------------------------------
# `az login` ouvre le navigateur (compte Entra ID Textinord). En pipeline Azure DevOps, on utilise
# une connexion de service (identité fédérée), jamais un mot de passe dans le YAML (module 6).
az account show > /dev/null 2>&1 || az login
az account set --subscription "${AZURE_SUBSCRIPTION_ID:-$(az account show --query id -o tsv)}"

# ----- 1. Groupe de ressources --------------------------------------------------------------
# Tout le TP vit dans un seul groupe : un `az group delete` suffit à tout nettoyer (coût maîtrisé).
az group create --name "$RG" --location "$LOCATION" --tags projet=trame2 env=dev proprietaire=sofia.marques

# ----- 2. Registre de containers (ACR) ------------------------------------------------------
# SKU Basic : suffisant pour dev/test. L'image y est poussée directement par le SDK .NET.
az acr create --resource-group "$RG" --name "$ACR" --sku Basic --admin-enabled false
az acr login --name "$ACR"

# ----- 3. Construire ET pousser l'image sans Docker -----------------------------------------
# Le SDK .NET construit l'image à partir des propriétés du csproj (base aspnet:10.0, port 8080,
# utilisateur app) et la pousse vers le registre indiqué. Pas de Dockerfile, pas de démon.
dotnet publish "$API_PROJECT" -c Release /t:PublishContainer \
  -p ContainerRegistry="$ACR.azurecr.io" \
  -p ContainerRepository="$IMAGE_NAME" \
  -p ContainerImageTags="$IMAGE_TAG"

# ----- 4. Journalisation et environnement Container Apps ------------------------------------
# L'environnement est le « cluster » managé : réseau, logs, certificats. On y déploie ensuite
# autant d'applications que nécessaire (API, extranet Blazor, worker préparations).
az monitor log-analytics workspace create --resource-group "$RG" --workspace-name "$LAW_NAME" --location "$LOCATION"
LAW_ID=$(az monitor log-analytics workspace show --resource-group "$RG" --workspace-name "$LAW_NAME" --query customerId -o tsv)
LAW_KEY=$(az monitor log-analytics workspace get-shared-keys --resource-group "$RG" --workspace-name "$LAW_NAME" --query primarySharedKey -o tsv)

az containerapp env create \
  --name "$ENV_NAME" --resource-group "$RG" --location "$LOCATION" \
  --logs-workspace-id "$LAW_ID" --logs-workspace-key "$LAW_KEY"

# ----- 5. Identité managée pour tirer l'image (pas de mot de passe ACR) ---------------------
# L'application container reçoit une identité Entra ID ; on lui donne le rôle AcrPull sur le registre.
ACR_ID=$(az acr show --name "$ACR" --resource-group "$RG" --query id -o tsv)

# ----- 6. Déploiement de l'application ------------------------------------------------------
# - ingress externe sur le port 8080 (TLS terminé par Azure, certificat automatique)
# - 1 à 3 réplicas : montée en charge sur le nombre de requêtes HTTP (900 commandes/jour en pointe,
#   c'est surtout l'extranet et les scanners qui lisent)
# - variables d'environnement : environnement ASP.NET Core et origines CORS de l'extranet
az containerapp create \
  --name "$APP_NAME" --resource-group "$RG" --environment "$ENV_NAME" \
  --image "$ACR.azurecr.io/$IMAGE_NAME:$IMAGE_TAG" \
  --registry-server "$ACR.azurecr.io" --registry-identity system \
  --system-assigned \
  --target-port 8080 --ingress external \
  --min-replicas 1 --max-replicas 3 \
  --cpu 0.5 --memory 1.0Gi \
  --env-vars ASPNETCORE_ENVIRONMENT=Production "Cors__Origines__0=https://extranet.textinord.example" \
  --scale-rule-name http-concurrence --scale-rule-type http --scale-rule-http-concurrency 50

# Droit AcrPull pour l'identité système créée ci-dessus.
PRINCIPAL_ID=$(az containerapp show --name "$APP_NAME" --resource-group "$RG" --query identity.principalId -o tsv)
az role assignment create --assignee-object-id "$PRINCIPAL_ID" --assignee-principal-type ServicePrincipal \
  --role AcrPull --scope "$ACR_ID"

# ----- 7. Sondes de santé -------------------------------------------------------------------
# Container Apps interroge /health/live (redémarrage si KO) et /health/ready (retrait du trafic si KO).
# Les sondes se déclarent dans le YAML de l'application ; on l'exporte, on le complète, on le réapplique.
az containerapp show --name "$APP_NAME" --resource-group "$RG" --output yaml > /tmp/ca-trame-api.yaml
python3 - <<'PY'
import yaml
chemin = "/tmp/ca-trame-api.yaml"
app = yaml.safe_load(open(chemin))
conteneur = app["properties"]["template"]["containers"][0]
conteneur["probes"] = [
    {"type": "Liveness",  "httpGet": {"path": "/health/live",  "port": 8080}, "initialDelaySeconds": 5,  "periodSeconds": 10},
    {"type": "Readiness", "httpGet": {"path": "/health/ready", "port": 8080}, "initialDelaySeconds": 5,  "periodSeconds": 10},
]
yaml.safe_dump(app, open(chemin, "w"))
PY
az containerapp update --name "$APP_NAME" --resource-group "$RG" --yaml /tmp/ca-trame-api.yaml

# ----- 8. Vérification ----------------------------------------------------------------------
FQDN=$(az containerapp show --name "$APP_NAME" --resource-group "$RG" --query properties.configuration.ingress.fqdn -o tsv)
echo "API déployée : https://$FQDN"
curl -fsS "https://$FQDN/health/ready" && echo
curl -fsS "https://$FQDN/api/v1/referentiel/statuts" | head -c 300 && echo

# En production, l'authentification passe par Entra ID (Microsoft.Identity.Web) : la section
# AzureAd (TenantId, ClientId, Audience) se fournit par variables d'environnement ou Key Vault,
# jamais dans l'image. Le jeton `dotnet user-jwts` du poste de développement ne sera pas accepté ici.

# ----- Alternative : azd --------------------------------------------------------------------
# Avec l'AppHost Aspire, tout ce qui précède se résume à :
#   azd init      (choisir « Use code in the current directory », l'AppHost est détecté)
#   azd up        (crée ACR, environnement Container Apps, identités, déploie chaque projet)
#   azd down      (supprime tout)
# Le script ci-dessus reste utile pour comprendre ce qu'azd fait à votre place.
