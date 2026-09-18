using Textinord.Trame.Domain.Tarifs;

namespace Textinord.Trame.Domain.Tests;

public sealed class PolitiqueRemiseTests
{
    [Fact]
    public void Remise_client_seule_sous_le_seuil_de_volume()
    {
        var remise = PolitiqueRemise.Calculer(ConditionTarifaire.GrandCompte, quantite: 100);

        Assert.Equal(18m, remise);
    }

    [Fact]
    public void Remise_volume_de_cinq_pour_cent_au_dela_de_500_pieces()
    {
        var remise = PolitiqueRemise.Calculer(new ConditionTarifaire("PME", 10m), quantite: 501);

        Assert.Equal(15m, remise);
    }

    [Fact]
    public void Cumul_plafonne_a_trente_pour_cent()
    {
        var remise = PolitiqueRemise.Calculer(new ConditionTarifaire("MAX", 25m), quantite: 1_000);

        Assert.Equal(30m, remise);
    }

    [Fact]
    public void Condition_tarifaire_refuse_plus_de_vingt_cinq_pour_cent()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConditionTarifaire("TROP", 26m));
    }
}
