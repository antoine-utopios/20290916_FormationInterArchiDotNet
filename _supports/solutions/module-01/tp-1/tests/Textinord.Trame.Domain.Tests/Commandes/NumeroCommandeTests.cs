using Textinord.Trame.Domain.Commandes;

namespace Textinord.Trame.Domain.Tests.Commandes;

public class NumeroCommandeTests
{
    [Fact]
    public void Le_numero_respecte_le_format_CMD_AAAA_NNNNNN()
    {
        var numero = new NumeroCommande(2026, 42);

        Assert.Equal("CMD-2026-000042", numero.Valeur);
    }

    [Fact]
    public void Le_generateur_est_sequentiel_et_repart_a_1_chaque_annee()
    {
        var generateur = new GenerateurNumeroCommandeEnMemoire();

        var premier = generateur.Suivant(2026);
        var deuxieme = generateur.Suivant(2026);
        var nouvelleAnnee = generateur.Suivant(2027);

        Assert.Equal("CMD-2026-000001", premier.Valeur);
        Assert.Equal("CMD-2026-000002", deuxieme.Valeur);
        Assert.Equal("CMD-2027-000001", nouvelleAnnee.Valeur);
    }

    [Theory]
    [InlineData("CMD-2026-000042", true)]
    [InlineData("cmd-2026-000042", false)]
    [InlineData("CMD-26-42", false)]
    [InlineData("CMD-2026-000000", false)]
    [InlineData("", false)]
    public void Le_parseur_accepte_uniquement_le_format_officiel(string texte, bool attendu)
    {
        var resultat = NumeroCommande.Parser(texte);

        Assert.Equal(attendu, resultat.EstSucces);
        if (attendu)
        {
            Assert.Equal(texte, resultat.Valeur.Valeur);
        }
    }
}
