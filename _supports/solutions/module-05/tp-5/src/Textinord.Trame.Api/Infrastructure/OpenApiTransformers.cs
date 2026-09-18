using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Textinord.Trame.Api.Infrastructure;

/// <summary>
/// Enrichit chaque document OpenAPI (v1, v2) : métadonnées, schéma de sécurité Bearer.
/// </summary>
public sealed class DocumentTrameTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);

        document.Info = new OpenApiInfo
        {
            Title = "Trame 2 — API commandes",
            Version = context.DocumentName,
            Description = "API des commandes Textinord, exposée à l'extranet, à l'application entrepôt et aux partenaires EDI. "
                        + "Authentification par jeton Bearer (Entra ID en production, dotnet user-jwts en local).",
            Contact = new OpenApiContact { Name = "Sofia Marques — lead développeuse", Email = "sofia.marques@textinord.example" },
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Jeton JWT émis par Entra ID (production) ou par `dotnet user-jwts create` (développement).",
        };

        return Task.CompletedTask;
    }
}

/// <summary>
/// Ajoute l'exigence de sécurité Bearer sur chaque opération protégée par une politique d'autorisation,
/// pour que Scalar propose le champ « jeton » au bon endroit et que les clients générés (Kiota) l'exigent.
/// </summary>
public sealed class ExigenceBearerTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var metadonnees = context.Description.ActionDescriptor.EndpointMetadata;
        var protegee = metadonnees.OfType<IAuthorizeData>().Any();
        var anonyme = metadonnees.OfType<IAllowAnonymous>().Any();

        if (protegee && !anonyme)
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = [],
            });
        }

        return Task.CompletedTask;
    }
}
