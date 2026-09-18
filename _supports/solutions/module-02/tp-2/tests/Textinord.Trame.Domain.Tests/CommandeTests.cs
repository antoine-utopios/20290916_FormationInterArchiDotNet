using Textinord.Trame.Domain.Commandes;
using Textinord.Trame.Domain.Entrepots;

namespace Textinord.Trame.Domain.Tests;

public sealed class CommandeTests
{
    private static readonly DateTimeOffset Date = new(2026, 9, 7, 9, 0, 0, TimeSpan.FromHours(2));

    [Fact]
    public void Validation_reussie_quand_chaque_ligne_a_du_stock()
    {
        var commande = CommandeDeTest();

        commande.Valider(new StockFixe(disponible: true));

        Assert.Equal(StatutCommande.Validee, commande.Statut);
    }

    [Fact]
    public void Passe_en_attente_de_stock_quand_une_ligne_est_en_rupture()
    {
        var commande = CommandeDeTest();

        commande.Valider(new StockFixe(disponible: false));

        Assert.Equal(StatutCommande.EnAttenteStock, commande.Statut);
    }

    [Fact]
    public void Annulation_interdite_apres_le_debut_de_la_preparation()
    {
        var commande = CommandeDeTest();
        commande.Valider(new StockFixe(disponible: true));
        commande.DemarrerPreparation();

        var exception = Assert.Throws<InvalidOperationException>(commande.Annuler);

        Assert.Contains("annulation impossible", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Numero_de_commande_invalide_est_refuse()
    {
        Assert.Throws<ArgumentException>(() => new NumeroCommande("2026-000001"));
        Assert.Equal("CMD-2026-000042", NumeroCommande.Generer(2026, 42).Valeur);
    }

    private static Commande CommandeDeTest()
    {
        var commande = new Commande(NumeroCommande.Generer(2026, 1), "CLI-000318", "fr", Date);
        commande.AjouterLigne(new LigneCommande("VT-BLOUSE-M", 120, 18.50m, 12m, Entrepot.Roubaix.Code));
        commande.AjouterLigne(new LigneCommande("EPI-GANT-L", 600, 3.20m, 17m, Entrepot.Lesquin.Code));
        return commande;
    }

    private sealed class StockFixe(bool disponible) : IDisponibiliteStock
    {
        public bool EstDisponible(string reference, int quantite) => disponible;
    }
}
