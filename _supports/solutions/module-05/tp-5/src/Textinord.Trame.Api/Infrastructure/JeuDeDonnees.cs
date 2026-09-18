using Textinord.Trame.Api.Commandes;

namespace Textinord.Trame.Api.Infrastructure;

/// <summary>
/// Amorce le store en mémoire avec trois commandes représentatives : une validée (stock disponible),
/// un brouillon, une en attente de stock (gants EPI-GANT-09 en rupture dans les deux entrepôts).
/// </summary>
public static class JeuDeDonnees
{
    public static void Amorcer(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        using var scope = services.CreateScope();
        var commandes = scope.ServiceProvider.GetRequiredService<CommandesService>();

        var validee = commandes.Creer(new CreerCommandeRequete("CLI-0042",
        [
            new LigneRequete("VT-PARKA-XL", 120),
            new LigneRequete("EPI-GILET-L", 600),
        ]));
        commandes.Valider(validee.Commande!);

        commandes.Creer(new CreerCommandeRequete("CLI-0107",
        [
            new LigneRequete("HOT-DRAP-160", 200, PrixNegocie: 17.90m),
            new LigneRequete("HOT-SERV-50", 400),
        ]));

        var enAttente = commandes.Creer(new CreerCommandeRequete("CLI-0311",
        [
            new LigneRequete("EPI-GANT-09", 250),
        ]));
        commandes.Valider(enAttente.Commande!);
    }
}
