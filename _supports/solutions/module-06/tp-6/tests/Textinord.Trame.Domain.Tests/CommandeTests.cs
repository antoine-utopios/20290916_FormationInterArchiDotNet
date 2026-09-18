using Textinord.Trame.Domain;

namespace Textinord.Trame.Domain.Tests;

public class CommandeTests
{
    private static readonly DateTimeOffset Le1erSeptembre2026 = new(2026, 9, 1, 8, 0, 0, TimeSpan.FromHours(2));

    private static LigneCommande Ligne(string reference, int quantite, decimal prix = 10m, decimal tauxClient = 0.10m)
        => new(reference, quantite, prix, tauxClient);

    private static Commande CommandeDeuxLignes()
        => Commande.Creer("CMD-2026-000042", "C-0001", Le1erSeptembre2026,
            [Ligne("VT-1001", 120), Ligne("EPI-2040", 30)]).Valeur!;

    [Fact]
    public void Creer_refuse_une_commande_sans_ligne()
    {
        var resultat = Commande.Creer("CMD-2026-000001", "C-0001", Le1erSeptembre2026, []);

        Assert.False(resultat.EstSucces);
        Assert.Contains("au moins une ligne", resultat.Erreurs.Single());
    }

    [Fact]
    public void Creer_refuse_un_numero_mal_forme()
    {
        var resultat = Commande.Creer("2026-42", "C-0001", Le1erSeptembre2026, [Ligne("VT-1001", 1)]);

        Assert.False(resultat.EstSucces);
        Assert.Contains(resultat.Erreurs, e => e.Contains("Numéro de commande invalide"));
    }

    [Fact]
    public void Numero_est_au_format_CMD_annee_sequence_sur_six_chiffres()
    {
        Assert.Equal("CMD-2026-000123", NumeroCommande.Formater(2026, 123));
        Assert.True(NumeroCommande.EstValide("CMD-2026-000123"));
        Assert.False(NumeroCommande.EstValide("CMD-26-123"));
    }

    [Fact]
    public void Valider_affecte_un_entrepot_a_chaque_ligne_et_emet_l_evenement()
    {
        var commande = CommandeDeuxLignes();
        var affectations = new Dictionary<string, string?>
        {
            ["VT-1001"] = Entrepots.Roubaix,
            ["EPI-2040"] = Entrepots.Lesquin,
        };

        var evenement = commande.Valider(affectations, Le1erSeptembre2026);

        Assert.Equal(StatutCommande.Validee, commande.Statut);
        Assert.Equal(Le1erSeptembre2026, commande.ValideeLe);
        Assert.NotNull(evenement);
        Assert.Equal("CMD-2026-000042", evenement.Numero);
        Assert.Collection(evenement.Lignes,
            l => Assert.Equal(("VT-1001", 120, "RBX"), (l.Reference, l.Quantite, l.Entrepot)),
            l => Assert.Equal(("EPI-2040", 30, "LSQ"), (l.Reference, l.Quantite, l.Entrepot)));
    }

    [Fact]
    public void Valider_passe_en_attente_stock_quand_une_ligne_n_a_aucun_entrepot()
    {
        var commande = CommandeDeuxLignes();
        var affectations = new Dictionary<string, string?>
        {
            ["VT-1001"] = Entrepots.Roubaix,
            ["EPI-2040"] = null,
        };

        var evenement = commande.Valider(affectations, Le1erSeptembre2026);

        Assert.Null(evenement);
        Assert.Equal(StatutCommande.EnAttenteStock, commande.Statut);
        Assert.Null(commande.ValideeLe);
        Assert.All(commande.Lignes, l => Assert.Null(l.EntrepotAffecte));
    }

    [Fact]
    public void MontantNet_additionne_les_lignes_remisees()
    {
        // 100 × 10 € à 10 % = 900 € ; 600 × 10 € à 15 % (10 % + 5 % volume) = 5 100 €
        var commande = Commande.Creer("CMD-2026-000007", "C-0001", Le1erSeptembre2026,
            [Ligne("VT-1001", 100), Ligne("LH-3300", 600)]).Valeur!;

        Assert.Equal(6000m, commande.MontantNet);
    }

    [Fact]
    public void Annuler_est_impossible_une_fois_la_preparation_commencee()
    {
        var commande = CommandeDeuxLignes();
        commande.Valider(new Dictionary<string, string?> { ["VT-1001"] = "RBX", ["EPI-2040"] = "LSQ" }, Le1erSeptembre2026);
        commande.DemarrerPreparation();

        var exception = Assert.Throws<InvalidOperationException>(commande.Annuler);

        Assert.Contains("annulation impossible", exception.Message);
        Assert.Equal(StatutCommande.EnPreparation, commande.Statut);
    }

    [Fact]
    public void Annuler_est_possible_avant_la_preparation()
    {
        var commande = CommandeDeuxLignes();

        commande.Annuler();

        Assert.Equal(StatutCommande.Annulee, commande.Statut);
    }
}
