namespace Textinord.Trame.Api.Infrastructure;

/// <summary>Politiques de cache de sortie. Le référentiel change rarement : 5 minutes, pour tous les appelants.</summary>
public static class CacheTrame
{
    public const string PolitiqueReferentiel = "referentiel";

    public static IServiceCollection AjouterCacheTrame(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOutputCache(options =>
        {
            options.AddPolicy(PolitiqueReferentiel, politique => politique
                .Expire(TimeSpan.FromMinutes(5))
                .Tag("referentiel"));
        });

        return services;
    }
}
