using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Textinord.Trame.Domain.Events;
using Textinord.Trame.Worker.Consumers;

namespace Textinord.Trame.Worker.Tests;

/// <summary>Le ConsumeContext est un substitut : on teste le consumer sans bus.</summary>
public sealed class OrdrePreparationConsumerTests : IDisposable
{
    private static readonly DateTimeOffset Instant = new(2026, 9, 7, 15, 0, 0, TimeSpan.FromHours(2));

    private readonly SqliteEnMemoire _base = new();
    private readonly TimeProvider _horloge = Substitute.For<TimeProvider>();

    public OrdrePreparationConsumerTests()
    {
        _horloge.GetUtcNow().Returns(Instant);
    }

    public void Dispose() => _base.Dispose();

    private static CommandeValidee CommandeSurDeuxEntrepots() => new(
        Guid.NewGuid(), "CMD-2026-000010", "C-0042", Instant,
        [
            new LigneAPreparer("VT-1001", 400, "RBX"),
            new LigneAPreparer("LH-3300", 250, "RBX"),
            new LigneAPreparer("EPI-2040", 60, "LSQ"),
        ]);

    private static ConsumeContext<CommandeValidee> Contexte(CommandeValidee message)
    {
        var contexte = Substitute.For<ConsumeContext<CommandeValidee>>();
        contexte.Message.Returns(message);
        contexte.MessageId.Returns(Guid.NewGuid());
        contexte.CancellationToken.Returns(CancellationToken.None);
        return contexte;
    }

    private async Task ConsommerAsync(CommandeValidee message)
    {
        await using var db = _base.CreerContexte();
        var consumer = new OrdrePreparationConsumer(db, _horloge, NullLogger<OrdrePreparationConsumer>.Instance);
        await consumer.Consume(Contexte(message));
    }

    [Fact]
    public async Task Cree_un_ordre_par_entrepot_concerne()
    {
        var message = CommandeSurDeuxEntrepots();

        await ConsommerAsync(message);

        await using var db = _base.CreerContexte();
        var ordres = await db.OrdresPreparation.OrderBy(o => o.Entrepot).ToListAsync();
        Assert.Collection(ordres,
            lesquin =>
            {
                Assert.Equal("LSQ", lesquin.Entrepot);
                Assert.Equal(1, lesquin.NombreLignes);
                Assert.Equal(60, lesquin.NombrePieces);
            },
            roubaix =>
            {
                Assert.Equal("RBX", roubaix.Entrepot);
                Assert.Equal(2, roubaix.NombreLignes);
                Assert.Equal(650, roubaix.NombrePieces);
                Assert.Equal(Instant, roubaix.EmisLe);
            });
    }

    [Fact]
    public async Task Est_idempotent_quand_le_meme_message_est_rejoue()
    {
        var message = CommandeSurDeuxEntrepots();

        await ConsommerAsync(message);
        await ConsommerAsync(message);
        await ConsommerAsync(message);

        await using var db = _base.CreerContexte();
        Assert.Equal(2, await db.OrdresPreparation.CountAsync(o => o.CommandeId == message.CommandeId));
    }
}
