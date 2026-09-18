# syntax=docker/dockerfile:1
# ---------------------------------------------------------------------------------------------
# Image de l'API Trame 2 — build multi-stage.
#   docker build -t textinord/trame-api:1.0.0 .
#   docker run --rm -p 8080:8080 -e ASPNETCORE_ENVIRONMENT=Development textinord/trame-api:1.0.0
#
# Alternative sans Dockerfile ni démon Docker (même résultat, voir le csproj) :
#   dotnet publish src/Textinord.Trame.Api -c Release /t:PublishContainer
# ---------------------------------------------------------------------------------------------

# Étape 1 : compilation et publication avec le SDK complet (≈ 900 Mo, jamais livré en production).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# On copie d'abord les fichiers projet seuls : la couche « restore » est mise en cache tant que
# les références NuGet ne changent pas, ce qui rend les builds suivants beaucoup plus rapides.
COPY Directory.Build.props ./
COPY src/Textinord.Trame.Api/Textinord.Trame.Api.csproj src/Textinord.Trame.Api/
RUN dotnet restore src/Textinord.Trame.Api/Textinord.Trame.Api.csproj

# Puis le code, et la publication en Release, sans re-restaurer.
COPY src/Textinord.Trame.Api/ src/Textinord.Trame.Api/
RUN dotnet publish src/Textinord.Trame.Api/Textinord.Trame.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Étape 2 : image d'exécution minimale (runtime ASP.NET Core seul, ≈ 220 Mo, utilisateur non root).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Le container n'écoute qu'en HTTP : TLS est terminé par l'ingress de Container Apps.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=0
EXPOSE 8080

# Utilisateur « app » (UID 1654) créé par l'image de base : jamais root en production.
USER $APP_UID

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Textinord.Trame.Api.dll"]
