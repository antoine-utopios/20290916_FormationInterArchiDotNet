using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Textinord.Trame.Api.Infrastructure;

/// <summary>Options de limitation de débit, liées à la section « RateLimiting » de la configuration (Options pattern).</summary>
public sealed class RateLimitingOptions
{
    public const string Section = "RateLimiting";

    /// <summary>Requêtes autorisées par fenêtre et par appelant (utilisateur authentifié, sinon adresse IP).</summary>
    [Range(1, 100_000)]
    public int PermitLimit { get; set; } = 100;

    [Range(1, 3_600)]
    public int FenetreSecondes { get; set; } = 10;
}

/// <summary>
/// Limitation de débit par appelant : protège l'API des clients EDI mal réglés (un partenaire a déjà
/// envoyé 20 000 requêtes en dix minutes sur Trame) sans pénaliser les autres.
/// Les options sont validées au démarrage et lues paresseusement : la configuration reste la source de vérité.
/// </summary>
public static class RateLimitingTrame
{
    public const string PolitiqueApi = "api";

    public static IServiceCollection AjouterRateLimitingTrame(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(limiteur => limiteur.RejectionStatusCode = StatusCodes.Status429TooManyRequests);

        // Les options du limiteur dépendent des nôtres : on les configure via IOptions, résolu au premier usage.
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingOptions>>((limiteur, options) =>
            {
                var reglages = options.Value;

                limiteur.AddPolicy(PolitiqueApi, contexte =>
                {
                    var appelant = contexte.User.Identity?.IsAuthenticated == true
                        ? contexte.User.Identity.Name ?? "authentifie"
                        : contexte.Connection.RemoteIpAddress?.ToString() ?? "anonyme";

                    return RateLimitPartition.GetFixedWindowLimiter(appelant, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = reglages.PermitLimit,
                        Window = TimeSpan.FromSeconds(reglages.FenetreSecondes),
                        QueueLimit = 0,
                    });
                });

                limiteur.OnRejected = async (contexte, jeton) =>
                {
                    contexte.HttpContext.Response.Headers.RetryAfter = reglages.FenetreSecondes.ToString(CultureInfo.InvariantCulture);

                    var problemes = contexte.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                    await problemes.WriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = contexte.HttpContext,
                        ProblemDetails =
                        {
                            Status = StatusCodes.Status429TooManyRequests,
                            Type = Problemes.BaseType + "trop-de-requetes",
                            Title = "Trop de requêtes",
                            Detail = $"Limite de {reglages.PermitLimit} requêtes par {reglages.FenetreSecondes} s atteinte pour cet appelant.",
                        },
                    });
                };
            });

        return services;
    }
}
