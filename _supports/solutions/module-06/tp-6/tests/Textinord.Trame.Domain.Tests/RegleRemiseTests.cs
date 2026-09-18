using Textinord.Trame.Domain;

namespace Textinord.Trame.Domain.Tests;

public class RegleRemiseTests
{
    [Theory]
    [InlineData(0.10, 100, 0.10)]   // remise client seule
    [InlineData(0.10, 501, 0.15)]   // + 5 % de remise volume au-delà de 500 pièces
    [InlineData(0.25, 500, 0.25)]   // 500 pièces exactement : pas de remise volume
    [InlineData(0.25, 600, 0.30)]   // 25 % + 5 % = 30 %, le plafond
    [InlineData(0.00, 1000, 0.05)]  // client sans condition tarifaire, volume seul
    public void Cumule_la_remise_client_et_la_remise_volume_avec_un_plafond_de_30_pour_cent(
        double tauxClient, int quantite, double attendu)
    {
        var taux = RegleRemise.Calculer((decimal)tauxClient, quantite);

        Assert.Equal((decimal)attendu, taux);
    }

    [Fact]
    public void Refuse_un_taux_client_superieur_a_25_pour_cent()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RegleRemise.Calculer(0.26m, 10));
    }
}
