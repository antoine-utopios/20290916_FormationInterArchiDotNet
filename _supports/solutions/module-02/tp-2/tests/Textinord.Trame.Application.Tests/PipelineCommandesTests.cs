using Textinord.Trame.Application.Commandes;
using Textinord.Trame.Application.Commandes.Messages;
using Textinord.Trame.Application.Commandes.Pipeline;
using Textinord.Trame.Application.Commandes.Traduction;
using Textinord.Trame.Application.Messaging;
using Textinord.Trame.Application.Tests.Outils;
using Textinord.Trame.Domain.Entrepots;

namespace Textinord.Trame.Application.Tests;

public sealed class PipelineCommandesTests
{
    [Fact]
    public async Task Une_commande_france_sur_deux_entrepots_produit_deux_ordres_sequences_et_correles()
    {
        using var deadLetters = new DeadLetterChannel();
        await using var bus = new InProcessMessageBus(deadLetters, new HorlogeFixe(Fabrique.Depart));
        var gateway = new EntrepotGatewayEnMemoire();
        var historique = new HistoriqueMessages();
        _ = new PipelineCommandes(bus, gateway, historique, new OrdrePreparationTranslator());

        var evenement = Fabrique.Enveloppe(Fabrique.CommandeFrance(), dureeDeVie: TimeSpan.FromHours(4));
        await bus.PublierEnveloppeAsync(CanauxTrame.CommandesValidees, evenement);
        await bus.DemarrerAsync();
        await bus.ArreterAsync();

        Assert.Empty(deadLetters.Lettres);
        Assert.Equal(2, gateway.Recus.Count);
        Assert.Contains(gateway.Recus, r => r.Entrepot == Entrepot.Roubaix && r.Ordre.NumeroOrdre == "OP-2026-001042-RBX");
        Assert.Contains(gateway.Recus, r => r.Entrepot == Entrepot.Lesquin && r.Ordre.NumeroOrdre == "OP-2026-001042-LSQ");

        var trace = historique.ParCorrelation(evenement.CorrelationId);
        Assert.Equal(3, trace.Count);
        Assert.All(trace, etape => Assert.Equal(evenement.CorrelationId, etape.Message.CorrelationId));
        var ordres = trace.Where(e => e.Message.EstDeType<OrdrePreparationEntrepot>()).ToList();
        Assert.Equal([1, 2], ordres.Select(o => o.Message.NumeroSequence!.Value).Order());
        Assert.All(ordres, o => Assert.Equal(2, o.Message.TailleSequence));
        Assert.All(ordres, o => Assert.Equal(evenement.MessageId, o.Message.CausationId));
    }

    [Fact]
    public async Task Une_commande_export_passe_par_le_flux_export_et_atteint_lesquin()
    {
        using var deadLetters = new DeadLetterChannel();
        await using var bus = new InProcessMessageBus(deadLetters);
        var gateway = new EntrepotGatewayEnMemoire();
        var historique = new HistoriqueMessages();
        _ = new PipelineCommandes(bus, gateway, historique, new OrdrePreparationTranslator());

        await bus.PublierAsync(CanauxTrame.CommandesValidees, Fabrique.CommandeExport());
        await bus.DemarrerAsync();
        await bus.ArreterAsync();

        var recu = Assert.Single(gateway.Recus);
        Assert.Equal(Entrepot.Lesquin, recu.Entrepot);
        Assert.Equal("EXPORT", recu.Ordre.TypeFlux);
        Assert.Equal("URGENTE", recu.Ordre.Priorite);
    }

    [Fact]
    public async Task Pays_absent_et_entrepot_inconnu_finissent_en_dead_letter_sans_rien_transmettre()
    {
        using var deadLetters = new DeadLetterChannel();
        await using var bus = new InProcessMessageBus(deadLetters);
        var gateway = new EntrepotGatewayEnMemoire();
        _ = new PipelineCommandes(bus, gateway, new HistoriqueMessages(), new OrdrePreparationTranslator());

        await bus.PublierAsync(CanauxTrame.CommandesValidees, Fabrique.CommandeSansPays());
        await bus.PublierAsync(CanauxTrame.CommandesValidees, Fabrique.CommandeEntrepotInconnu());
        await bus.DemarrerAsync();
        await bus.ArreterAsync();

        Assert.Empty(gateway.Recus);
        Assert.Equal(2, deadLetters.Nombre);
        Assert.Contains(deadLetters.Lettres, l => l.Canal == CanauxTrame.CommandesValidees && l.Raison.Contains("pays de livraison absent", StringComparison.Ordinal));
        Assert.Contains(deadLetters.Lettres, l => l.Canal == CanauxTrame.PreparationsFrance && l.Raison.Contains("PARIS", StringComparison.Ordinal));
    }

    [Fact]
    public void Les_canaux_sont_declares_de_l_amont_vers_l_aval()
    {
        using var deadLetters = new DeadLetterChannel();
        using var bus = new InProcessMessageBus(deadLetters);
        _ = new PipelineCommandes(bus, new EntrepotGatewayEnMemoire(), new HistoriqueMessages(), new OrdrePreparationTranslator());

        Assert.Equal(
            [CanauxTrame.CommandesValidees, CanauxTrame.PreparationsFrance, CanauxTrame.PreparationsExport, "entrepot.RBX", "entrepot.LSQ", CanauxTrame.Audit],
            bus.Canaux);
    }
}
