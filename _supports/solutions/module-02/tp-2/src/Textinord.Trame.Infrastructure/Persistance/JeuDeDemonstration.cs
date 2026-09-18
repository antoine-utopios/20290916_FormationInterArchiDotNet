using Textinord.Trame.Domain.Commandes;
using Textinord.Trame.Domain.Entrepots;

namespace Textinord.Trame.Infrastructure.Persistance;

/// <summary>Trois commandes réalistes : France sur deux entrepôts, export urgente, France mono-entrepôt.</summary>
public static class JeuDeDemonstration
{
    public static IReadOnlyList<Commande> Creer(DateTimeOffset maintenant)
    {
        var lille = new Commande(NumeroCommande.Generer(maintenant.Year, 1_042), "CLI-000318", "FR", maintenant);
        lille.AjouterLigne(new LigneCommande("VT-BLOUSE-M", 120, 18.50m, 12m, Entrepot.Roubaix.Code));
        lille.AjouterLigne(new LigneCommande("EPI-GANT-L", 600, 3.20m, 17m, Entrepot.Lesquin.Code));

        var bruxelles = new Commande(NumeroCommande.Generer(maintenant.Year, 1_043), "CLI-000502", "BE", maintenant, urgente: true);
        bruxelles.AjouterLigne(new LigneCommande("HOT-DRAP-240", 250, 9.90m, 18m, Entrepot.Lesquin.Code));

        var arras = new Commande(NumeroCommande.Generer(maintenant.Year, 1_044), "CLI-000077", "FR", maintenant);
        arras.AjouterLigne(new LigneCommande("VT-PANTALON-44", 40, 24.00m, 0m, Entrepot.Roubaix.Code));

        return [lille, bruxelles, arras];
    }
}
