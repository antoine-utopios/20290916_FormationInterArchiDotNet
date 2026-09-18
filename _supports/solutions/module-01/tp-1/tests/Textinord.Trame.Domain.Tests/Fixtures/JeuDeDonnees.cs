using Textinord.Trame.Domain.Catalogue;
using Textinord.Trame.Domain.Clients;
using Textinord.Trame.Domain.Commandes;

namespace Textinord.Trame.Domain.Tests.Fixtures;

/// <summary>
/// Jeu de données minimal et lisible : les tests racontent des situations Textinord,
/// pas des tableaux de nombres.
/// </summary>
internal static class JeuDeDonnees
{
    public static readonly DateOnly Rentree2026 = new(2026, 9, 7);

    public static Client MairieDeRoubaix() => new("C-00042", "Mairie de Roubaix", ConditionTarifaire.Collectivite);

    public static Client HotelDuBeffroi() => new("C-00108", "Hôtel du Beffroi", ConditionTarifaire.Hotellerie);

    public static Client AcieriesDeDenain() => new("C-00007", "Aciéries de Denain", ConditionTarifaire.GrandCompte);

    public static Client ClientStandard() => new("C-00999", "Atelier Dupont", ConditionTarifaire.Standard);

    /// <summary>Veste de travail : 300 à Roubaix, 800 à Lesquin.</summary>
    public static Article VesteDeTravail() =>
        new Article("VT-4410", "Veste de travail bleu marine", "Vêtements de travail", 32.50m)
            .AvecStock(Entrepot.Roubaix, 300)
            .AvecStock(Entrepot.Lesquin, 800);

    /// <summary>Gant anti-coupure : 2 000 à Roubaix seulement.</summary>
    public static Article GantAntiCoupure() =>
        new Article("EPI-2205", "Gant anti-coupure niveau 5", "EPI", 4.20m)
            .AvecStock(Entrepot.Roubaix, 2_000);

    /// <summary>Drap 240x300 : en rupture partout.</summary>
    public static Article DrapHotellerie() =>
        new Article("LH-0901", "Drap plat 240x300 blanc", "Linge hôtellerie", 18.90m)
            .AvecStock(Entrepot.Roubaix, 0)
            .AvecStock(Entrepot.Lesquin, 0);

    public static Commande NouvelleCommande(Client client) =>
        new(new NumeroCommande(2026, 1), client, Rentree2026);
}
