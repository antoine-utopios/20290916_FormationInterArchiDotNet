using Textinord.Trame.Domain.Clients;
using Textinord.Trame.Domain.Tarification;

namespace Textinord.Trame.Domain.Tests.Tarification;

public class CalculRemiseTests
{
    [Fact]
    public void Remise_client_seule_sous_le_seuil_de_volume()
    {
        var remise = CalculRemise.Calculer(ConditionTarifaire.Collectivite, quantite: 100);

        Assert.Equal(0.10m, remise.TauxClient);
        Assert.Equal(0m, remise.TauxVolume);
        Assert.Equal(0.10m, remise.TauxApplique);
        Assert.False(remise.Plafonnee);
    }

    [Theory]
    [InlineData(500, 0.00)]
    [InlineData(501, 0.05)]
    [InlineData(2_000, 0.05)]
    public void Remise_volume_a_partir_de_la_501e_piece(int quantite, decimal tauxVolumeAttendu)
    {
        var remise = CalculRemise.Calculer(ConditionTarifaire.Standard, quantite);

        Assert.Equal(tauxVolumeAttendu, remise.TauxVolume);
        Assert.Equal(tauxVolumeAttendu, remise.TauxApplique);
    }

    [Fact]
    public void Grand_compte_et_volume_se_cumulent_jusqu_au_plafond_de_30_pourcent()
    {
        var remise = CalculRemise.Calculer(ConditionTarifaire.GrandCompte, quantite: 600);

        Assert.Equal(0.25m, remise.TauxClient);
        Assert.Equal(0.05m, remise.TauxVolume);
        Assert.Equal(0.30m, remise.TauxApplique);
        Assert.False(remise.Plafonnee);
    }

    [Fact]
    public void Le_plafond_ecrete_un_cumul_superieur_a_30_pourcent()
    {
        // Le plafond est une ceinture de sécurité : si demain une condition passe à 28 %, il tient.
        var remise = CalculRemise.Calculer(tauxClient: 0.28m, quantite: 600);

        Assert.Equal(0.30m, remise.TauxApplique);
        Assert.True(remise.Plafonnee);
    }

    [Fact]
    public void Une_condition_tarifaire_ne_depasse_jamais_25_pourcent()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new ConditionTarifaire("XXL", 0.26m));

        Assert.Equal("tauxRemise", exception.ParamName);
    }
}
