using Microsoft.AspNetCore.Authorization;

namespace Textinord.Trame.Api.Securite;

/// <summary>
/// Les politiques d'autorisation de l'API, nommées une fois pour toutes.
/// Les rôles viennent du claim « role » du jeton : émis par `dotnet user-jwts --role` en local,
/// par les app roles d'Entra ID en production (module 6 pour la correspondance groupes → rôles).
/// </summary>
public static class Politiques
{
    /// <summary>Lire les commandes : l'ADV, les entrepôts (application scanner) et les partenaires EDI.</summary>
    public const string LectureCommandes = "commandes.lecture";

    /// <summary>Créer, valider, annuler : réservé à l'administration des ventes.</summary>
    public const string EcritureCommandes = "commandes.ecriture";

    public const string RoleAdv = "adv";

    public const string RoleEntrepot = "entrepot";

    public const string RolePartenaire = "partenaire";

    public static AuthorizationBuilder AjouterPolitiquesTrame(this AuthorizationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .AddPolicy(LectureCommandes, p => p
                .RequireAuthenticatedUser()
                .RequireRole(RoleAdv, RoleEntrepot, RolePartenaire))
            .AddPolicy(EcritureCommandes, p => p
                .RequireAuthenticatedUser()
                .RequireRole(RoleAdv));
    }
}
