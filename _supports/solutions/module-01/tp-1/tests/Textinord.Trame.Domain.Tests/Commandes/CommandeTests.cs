using Textinord.Trame.Domain.Catalogue;
using Textinord.Trame.Domain.Commandes;
using Textinord.Trame.Domain.Tests.Fixtures;

namespace Textinord.Trame.Domain.Tests.Commandes;

public class CommandeTests
{
    [Fact]
    public void Une_ligne_recoit_la_remise_du_client_et_le_montant_net_est_arrondi()
    {
        var commande = JeuDeDonnees.NouvelleCommande(JeuDeDonnees.MairieDeRoubaix());

        var ligne = commande.AjouterLigne(JeuDeDonnees.VesteDeTravail(), quantite: 10);

        Assert.True(ligne.EstSucces);
        Assert.Equal(0.10m, ligne.Valeur.Remise.TauxApplique);
        Assert.Equal(325.00m, ligne.Valeur.MontantBrut);
        Assert.Equal(292.50m, ligne.Valeur.MontantNet);
        Assert.Equal(292.50m, commande.TotalHT);
    }

    [Fact]
    public void Une_quantite_nulle_est_refusee_sans_exception()
    {
        var commande = JeuDeDonnees.NouvelleCommande(JeuDeDonnees.ClientStandard());

        var ligne = commande.AjouterLigne(JeuDeDonnees.VesteDeTravail(), quantite: 0);

        Assert.True(ligne.EstEchec);
        Assert.Equal("QUANTITE_INVALIDE", ligne.Erreur!.Code);
        Assert.Empty(commande.Lignes);
    }

    [Fact]
    public void La_validation_emet_un_ordre_de_preparation_par_entrepot_concerne()
    {
        var commande = JeuDeDonnees.NouvelleCommande(JeuDeDonnees.MairieDeRoubaix());
        commande.AjouterLigne(JeuDeDonnees.VesteDeTravail(), quantite: 500);   // Roubaix n'en a que 300 : Lesquin sert
        commande.AjouterLigne(JeuDeDonnees.GantAntiCoupure(), quantite: 1_000); // Roubaix seul en stock

        var resultat = commande.Valider();

        Assert.True(resultat.EstSucces);
        Assert.Equal(StatutCommande.Validee, commande.Statut);
        Assert.Equal(2, commande.OrdresPreparation.Count);
        Assert.Equal(Entrepot.Roubaix, commande.OrdresPreparation[0].Entrepot);
        Assert.Equal("EPI-2205", commande.OrdresPreparation[0].Lignes.Single().ReferenceArticle);
        Assert.Equal(Entrepot.Lesquin, commande.OrdresPreparation[1].Entrepot);
        Assert.Equal(Entrepot.Lesquin, commande.Lignes[0].EntrepotAffecte);
    }

    [Fact]
    public void Sans_stock_dans_aucun_entrepot_la_commande_passe_en_attente_de_stock()
    {
        var commande = JeuDeDonnees.NouvelleCommande(JeuDeDonnees.HotelDuBeffroi());
        commande.AjouterLigne(JeuDeDonnees.VesteDeTravail(), quantite: 10);
        commande.AjouterLigne(JeuDeDonnees.DrapHotellerie(), quantite: 50);

        var resultat = commande.Valider();

        Assert.True(resultat.EstSucces);
        Assert.Equal(StatutCommande.EnAttenteStock, resultat.Valeur);
        Assert.Empty(commande.OrdresPreparation);
        Assert.True(commande.EstModifiable);
    }

    [Fact]
    public void Une_commande_vide_ne_se_valide_pas()
    {
        var commande = JeuDeDonnees.NouvelleCommande(JeuDeDonnees.ClientStandard());

        var resultat = commande.Valider();

        Assert.True(resultat.EstEchec);
        Assert.Equal("COMMANDE_VIDE", resultat.Erreur!.Code);
        Assert.Equal(StatutCommande.Brouillon, commande.Statut);
    }

    [Fact]
    public void L_annulation_est_possible_avant_la_preparation_seulement()
    {
        var annulable = JeuDeDonnees.NouvelleCommande(JeuDeDonnees.ClientStandard());
        annulable.AjouterLigne(JeuDeDonnees.VesteDeTravail(), quantite: 5);
        annulable.Valider();

        var enPreparation = JeuDeDonnees.NouvelleCommande(JeuDeDonnees.ClientStandard());
        enPreparation.AjouterLigne(JeuDeDonnees.VesteDeTravail(), quantite: 5);
        enPreparation.Valider();
        enPreparation.DemarrerPreparation();

        Assert.True(annulable.Annuler().EstSucces);
        Assert.Equal(StatutCommande.Annulee, annulable.Statut);

        var refus = enPreparation.Annuler();
        Assert.True(refus.EstEchec);
        Assert.Equal("TRANSITION_INTERDITE", refus.Erreur!.Code);
        Assert.Equal(StatutCommande.EnPreparation, enPreparation.Statut);
    }

    [Fact]
    public void L_expedition_attend_la_cloture_de_tous_les_ordres()
    {
        var commande = JeuDeDonnees.NouvelleCommande(JeuDeDonnees.AcieriesDeDenain());
        commande.AjouterLigne(JeuDeDonnees.VesteDeTravail(), quantite: 600);
        commande.AjouterLigne(JeuDeDonnees.GantAntiCoupure(), quantite: 1_200);
        commande.Valider();
        commande.DemarrerPreparation();

        var roubaix = commande.OrdresPreparation.Single(o => o.Entrepot == Entrepot.Roubaix);
        var lesquin = commande.OrdresPreparation.Single(o => o.Entrepot == Entrepot.Lesquin);
        roubaix.Affecter("Marc Vandewalle");
        roubaix.Clore();

        var tropTot = commande.Expedier();
        Assert.True(tropTot.EstEchec);
        Assert.Equal("ORDRES_OUVERTS", tropTot.Erreur!.Code);

        lesquin.Affecter("Préparateur Lesquin");
        lesquin.Clore();

        Assert.True(commande.Expedier().EstSucces);
        Assert.Equal(StatutCommande.Expediee, commande.Statut);
        Assert.True(commande.Facturer().EstSucces);
        Assert.Equal(StatutCommande.Facturee, commande.Statut);
    }

    [Fact]
    public void Une_commande_validee_ne_prend_plus_de_ligne()
    {
        var commande = JeuDeDonnees.NouvelleCommande(JeuDeDonnees.ClientStandard());
        commande.AjouterLigne(JeuDeDonnees.VesteDeTravail(), quantite: 5);
        commande.Valider();

        var refus = commande.AjouterLigne(JeuDeDonnees.GantAntiCoupure(), quantite: 10);

        Assert.True(refus.EstEchec);
        Assert.Equal("COMMANDE_FIGEE", refus.Erreur!.Code);
        Assert.Single(commande.Lignes);
    }

    [Theory]
    [InlineData(StatutCommande.Brouillon, StatutCommande.Facturee, false)]
    [InlineData(StatutCommande.Brouillon, StatutCommande.Annulee, true)]
    [InlineData(StatutCommande.Validee, StatutCommande.EnPreparation, true)]
    [InlineData(StatutCommande.Expediee, StatutCommande.Annulee, false)]
    public void La_machine_a_etats_dit_ce_qui_est_permis(StatutCommande depart, StatutCommande cible, bool attendu)
    {
        var commande = JeuDeDonnees.NouvelleCommande(JeuDeDonnees.ClientStandard());
        commande.AjouterLigne(JeuDeDonnees.VesteDeTravail(), quantite: 5);
        AmenerA(commande, depart);

        Assert.Equal(depart, commande.Statut);
        Assert.Equal(attendu, commande.PeutPasserA(cible));
    }

    private static void AmenerA(Commande commande, StatutCommande statut)
    {
        if (statut == StatutCommande.Brouillon)
        {
            return;
        }

        commande.Valider();
        if (statut == StatutCommande.Validee)
        {
            return;
        }

        commande.DemarrerPreparation();
        foreach (var ordre in commande.OrdresPreparation)
        {
            ordre.Affecter("Préparateur");
            ordre.Clore();
        }

        commande.Expedier();
    }
}
