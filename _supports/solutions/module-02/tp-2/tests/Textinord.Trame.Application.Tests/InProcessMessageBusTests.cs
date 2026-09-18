using Textinord.Trame.Application.Messaging;
using Textinord.Trame.Application.Tests.Outils;

namespace Textinord.Trame.Application.Tests;

public sealed class InProcessMessageBusTests
{
    private static readonly TimeSpan Delai = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Un_message_expire_part_en_dead_letter_sans_etre_traite()
    {
        var horloge = new HorlogeFixe(Fabrique.Depart);
        using var deadLetters = new DeadLetterChannel();
        await using var bus = new InProcessMessageBus(deadLetters, horloge);
        var traites = 0;
        bus.Abonner("entree", (_, _, _) => { Interlocked.Increment(ref traites); return Task.CompletedTask; });

        await bus.PublierAsync("entree", "périmé", dureeDeVie: TimeSpan.FromMinutes(5));
        horloge.Avancer(TimeSpan.FromMinutes(6));
        await bus.DemarrerAsync();
        await bus.ArreterAsync();

        Assert.Equal(0, traites);
        var lettre = Assert.Single(deadLetters.Lettres);
        Assert.StartsWith("Message expiré", lettre.Raison, StringComparison.Ordinal);
        Assert.Equal("entree", lettre.Canal);
    }

    [Fact]
    public async Task Une_exception_de_handler_va_en_dead_letter_et_le_bus_continue()
    {
        using var deadLetters = new DeadLetterChannel();
        await using var bus = new InProcessMessageBus(deadLetters);
        var recus = new List<string>();
        bus.Abonner("entree", (message, _, _) =>
        {
            var corps = message.CorpsEnTantQue<string>();
            if (corps == "poison")
            {
                throw new InvalidOperationException("format inattendu");
            }

            recus.Add(corps);
            return Task.CompletedTask;
        });

        await bus.PublierAsync("entree", "un");
        await bus.PublierAsync("entree", "poison");
        await bus.PublierAsync("entree", "deux");
        await bus.DemarrerAsync();
        await bus.ArreterAsync();

        Assert.Equal(["un", "deux"], recus);
        var lettre = Assert.Single(deadLetters.Lettres);
        Assert.Equal("InvalidOperationException : format inattendu", lettre.Raison);
    }

    [Fact]
    public async Task Le_wire_tap_copie_le_message_sur_le_canal_d_audit_sans_le_modifier()
    {
        using var deadLetters = new DeadLetterChannel();
        await using var bus = new InProcessMessageBus(deadLetters);
        var historique = new HistoriqueMessages();
        bus.Abonner("entree", WireTap.Vers("audit"))
           .Abonner("entree", (_, _, _) => Task.CompletedTask)
           .Abonner("audit", historique.EnregistrerAsync);

        var original = Fabrique.Enveloppe("observé");
        await bus.PublierEnveloppeAsync("entree", original);
        await bus.DemarrerAsync();
        await bus.ArreterAsync();

        var copie = Assert.Single(historique.Messages);
        Assert.Equal("audit", copie.Canal);
        Assert.Same(original, copie.Message);
        Assert.Empty(deadLetters.Lettres);
    }

    [Fact]
    public async Task Publier_depuis_un_handler_propage_la_correlation_et_la_causation()
    {
        using var deadLetters = new DeadLetterChannel();
        await using var bus = new InProcessMessageBus(deadLetters);
        MessageEnvelope? sortie = null;
        bus.Abonner("entree", async (message, contexte, ct) =>
                await contexte.PublierAsync("sortie", $"traité : {message.CorpsEnTantQue<string>()}", ct))
           .Abonner("sortie", (message, _, _) => { sortie = message; return Task.CompletedTask; });

        var entree = Fabrique.Enveloppe("origine");
        await bus.PublierEnveloppeAsync("entree", entree);
        await bus.DemarrerAsync();
        await bus.ArreterAsync();

        Assert.NotNull(sortie);
        Assert.Equal(entree.CorrelationId, sortie.CorrelationId);
        Assert.Equal(entree.MessageId, sortie.CausationId);
        Assert.Equal("traité : origine", sortie.CorpsEnTantQue<string>());
    }

    [Fact]
    public async Task Un_canal_sans_abonne_envoie_en_dead_letter_et_un_canal_inconnu_leve_une_exception()
    {
        using var deadLetters = new DeadLetterChannel();
        await using var bus = new InProcessMessageBus(deadLetters);
        bus.DeclarerCanal("orphelin");

        await bus.PublierAsync("orphelin", "personne n'écoute");
        await Assert.ThrowsAsync<CanalInconnuException>(async () => await bus.PublierAsync("inexistant", "x"));
        await bus.DemarrerAsync();

        var lettre = await deadLetters.AttendreAsync(Delai);
        Assert.Equal("Aucun abonné sur ce canal", lettre.Raison);
    }
}
