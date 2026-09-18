using Textinord.Trame.Application.Commandes;
using Textinord.Trame.Application.Commandes.Routage;
using Textinord.Trame.Application.Tests.Outils;

namespace Textinord.Trame.Application.Tests;

public sealed class ContentBasedRouterTests
{
    [Fact]
    public void Commande_france_est_routee_vers_le_flux_national()
    {
        var decision = RoutageCommandes.CreerRouteur().Decider(Fabrique.Enveloppe(Fabrique.CommandeFrance()));

        Assert.Equal(CanauxTrame.PreparationsFrance, decision.Canal);
        Assert.Equal("France", decision.Regle);
    }

    [Fact]
    public void Commande_belge_est_routee_vers_le_flux_export()
    {
        var decision = RoutageCommandes.CreerRouteur().Decider(Fabrique.Enveloppe(Fabrique.CommandeExport()));

        Assert.Equal(CanauxTrame.PreparationsExport, decision.Canal);
    }

    [Fact]
    public void Pays_absent_est_un_rejet_explicite()
    {
        var decision = RoutageCommandes.CreerRouteur().Decider(Fabrique.Enveloppe(Fabrique.CommandeSansPays()));

        Assert.True(decision.EstRejet);
        Assert.Equal("pays de livraison absent", decision.Regle);
    }

    [Fact]
    public void Message_d_un_autre_type_sans_regle_ni_defaut_est_rejete()
    {
        var decision = RoutageCommandes.CreerRouteur().Decider(Fabrique.Enveloppe("un texte qui n'est pas une commande"));

        Assert.True(decision.EstRejet);
    }
}
