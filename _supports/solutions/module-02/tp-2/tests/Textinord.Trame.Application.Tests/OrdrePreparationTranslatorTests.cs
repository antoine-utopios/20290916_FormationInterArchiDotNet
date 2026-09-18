using Textinord.Trame.Application.Commandes.Messages;
using Textinord.Trame.Application.Commandes.Traduction;
using Textinord.Trame.Application.Messaging;
using Textinord.Trame.Application.Tests.Outils;

namespace Textinord.Trame.Application.Tests;

public sealed class OrdrePreparationTranslatorTests
{
    private readonly OrdrePreparationTranslator _traducteur = new();

    [Fact]
    public void Traduit_la_part_roubaix_d_une_commande_au_format_wms()
    {
        var ordre = _traducteur.Traduire(new CommandeParEntrepot(Fabrique.CommandeFrance(), "RBX"));

        Assert.Equal("OP-2026-001042-RBX", ordre.NumeroOrdre);
        Assert.Equal("ROUBAIX", ordre.Site);
        Assert.Equal("FRANCE", ordre.TypeFlux);
        Assert.Equal("NORMALE", ordre.Priorite);
        var ligne = Assert.Single(ordre.Lignes);
        Assert.Equal(new LignePreparation(1, "VT-BLOUSE-M", 120), ligne);
    }

    [Fact]
    public void Commande_export_urgente_porte_le_flux_et_la_priorite()
    {
        var ordre = _traducteur.Traduire(new CommandeParEntrepot(Fabrique.CommandeExport(), "lsq"));

        Assert.Equal("LESQUIN", ordre.Site);
        Assert.Equal("EXPORT", ordre.TypeFlux);
        Assert.Equal("URGENTE", ordre.Priorite);
    }

    [Fact]
    public void Entrepot_inconnu_leve_une_exception_de_traduction()
    {
        var exception = Assert.Throws<TraductionException>(
            () => _traducteur.Traduire(new CommandeParEntrepot(Fabrique.CommandeEntrepotInconnu(), "PARIS")));

        Assert.Contains("PARIS", exception.Message, StringComparison.Ordinal);
    }
}
