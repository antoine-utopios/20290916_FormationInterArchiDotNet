using Textinord.Extranet.Modeles;

namespace Textinord.Extranet.Tests.Services;

public class TarificationTests
{
    [Theory]
    [InlineData(10, 100, 10)]   // remise client seule
    [InlineData(10, 500, 10)]   // 500 pièces : le seuil n'est pas dépassé
    [InlineData(10, 501, 15)]   // au-delà de 500 : +5 %
    [InlineData(25, 600, 30)]   // 25 + 5 = 30, au plafond
    [InlineData(0, 2000, 5)]    // client sans remise, volume seul
    public void Le_taux_cumule_les_remises_avec_un_plafond(decimal remiseClient, int quantite, decimal attendu)
    {
        var taux = Tarification.CalculerTauxRemise(remiseClient, quantite);

        Assert.Equal(attendu, taux);
    }

    [Fact]
    public void Une_remise_client_hors_bornes_est_refusee()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Tarification.CalculerTauxRemise(26, 1));
    }
}
