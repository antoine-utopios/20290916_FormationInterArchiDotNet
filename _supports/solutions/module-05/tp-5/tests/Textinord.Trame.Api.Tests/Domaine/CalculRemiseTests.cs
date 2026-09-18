using Textinord.Trame.Api.Domaine;

namespace Textinord.Trame.Api.Tests.Domaine;

public sealed class CalculRemiseTests
{
    [Theory]
    [InlineData(0.00, 100, 0.00)]
    [InlineData(0.10, 500, 0.10)]
    [InlineData(0.10, 501, 0.15)]
    [InlineData(0.25, 600, 0.30)]
    [InlineData(0.25, 10, 0.25)]
    public void La_remise_cumule_client_et_volume_sous_le_plafond_de_30_pour_cent(decimal client, int quantite, decimal attendu)
    {
        var remise = CalculRemise.Calculer(client, quantite);

        Assert.Equal(attendu, remise.TauxApplique);
        Assert.Equal(client + remise.TauxVolume >= 0.30m, remise.PlafondAtteint);
    }

    [Fact]
    public void Une_commande_annulee_ne_se_valide_plus()
    {
        var commande = new Commande("CMD-2026-000042", "CLI-0042", 0.10m, new DateOnly(2026, 9, 9));
        commande.AjouterLigne("VT-PARKA-XL", 10, 64.90m);

        var annulation = commande.Annuler();
        var validation = commande.Valider((_, _) => "RBX");

        Assert.True(annulation.EstSucces);
        Assert.True(validation.EstEchec);
        Assert.Equal("TRANSITION_INTERDITE", validation.Erreur!.Code);
        Assert.Equal(StatutCommande.Annulee, commande.Statut);
    }
}
