using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute.ExceptionExtensions;
using Textinord.Trame.Domain.Events;
using Textinord.Trame.Infrastructure;
using Textinord.Trame.Infrastructure.Outbox;
using Textinord.Trame.Worker.Outbox;

namespace Textinord.Trame.Worker.Tests;

/// <summary>Tests unitaires du relais : le bus est un substitut NSubstitute, la base une SQLite en mémoire.</summary>
public sealed class OutboxRelayTests : IDisposable
{
    private static readonly DateTimeOffset Instant = new(2026, 9, 7, 14, 30, 0, TimeSpan.FromHours(2));

    private readonly SqliteEnMemoire _base = new();
    private readonly IPublishEndpoint _publication = Substitute.For<IPublishEndpoint>();
    private readonly TimeProvider _horloge = Substitute.For<TimeProvider>();

    public OutboxRelayTests()
    {
        _horloge.GetUtcNow().Returns(Instant);
    }

    public void Dispose() => _base.Dispose();

    private OutboxRelay CreerRelais(TrameDbContext db) =>
        new(db, _publication, _horloge, NullLogger<OutboxRelay>.Instance);

    private static CommandeValidee Evenement(string numero) =>
        new(Guid.NewGuid(), numero, "C-0001", Instant, [new LigneAPreparer("VT-1001", 120, "RBX")]);

    private async Task InsererAsync(params OutboxMessage[] messages)
    {
        await using var db = _base.CreerContexte();
        db.Outbox.AddRange(messages);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Publie_les_messages_en_attente_et_les_marque_envoyes()
    {
        await InsererAsync(
            OutboxSerialiseur.Emballer(Evenement("CMD-2026-000001"), Instant),
            OutboxSerialiseur.Emballer(Evenement("CMD-2026-000002"), Instant));

        await using var db = _base.CreerContexte();
        var envoyes = await CreerRelais(db).RelayerAsync(tailleLot: 50, tentativesMaximum: 10, CancellationToken.None);

        Assert.Equal(2, envoyes);
        await _publication.Received(2).Publish(
            Arg.Is<object>(m => m is CommandeValidee),
            typeof(CommandeValidee),
            Arg.Any<IPipe<PublishContext>>(),
            Arg.Any<CancellationToken>());
        Assert.All(await db.Outbox.ToListAsync(), m => Assert.Equal(Instant, m.EnvoyeLe));
    }

    [Fact]
    public async Task Ne_republie_pas_un_message_deja_envoye()
    {
        var dejaEnvoye = OutboxSerialiseur.Emballer(Evenement("CMD-2026-000003"), Instant);
        dejaEnvoye.EnvoyeLe = Instant.AddMinutes(-5);
        await InsererAsync(dejaEnvoye);

        await using var db = _base.CreerContexte();
        var envoyes = await CreerRelais(db).RelayerAsync(50, 10, CancellationToken.None);

        Assert.Equal(0, envoyes);
        await _publication.DidNotReceiveWithAnyArgs().Publish(default(object)!, default(Type)!, default(IPipe<PublishContext>)!, default);
    }

    [Fact]
    public async Task Compte_les_tentatives_et_conserve_le_message_quand_le_bus_est_injoignable()
    {
        await InsererAsync(OutboxSerialiseur.Emballer(Evenement("CMD-2026-000004"), Instant));
        _publication.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<IPipe<PublishContext>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("broker injoignable"));

        await using var db = _base.CreerContexte();
        var envoyes = await CreerRelais(db).RelayerAsync(50, 10, CancellationToken.None);

        Assert.Equal(0, envoyes);
        var message = await db.Outbox.SingleAsync();
        Assert.Null(message.EnvoyeLe);
        Assert.Equal(1, message.Tentatives);
        Assert.Contains("broker injoignable", message.DerniereErreur);
    }

    [Fact]
    public async Task Abandonne_un_message_qui_a_atteint_le_nombre_maximum_de_tentatives()
    {
        var empoisonne = OutboxSerialiseur.Emballer(Evenement("CMD-2026-000005"), Instant);
        empoisonne.Tentatives = 10;
        await InsererAsync(empoisonne);

        await using var db = _base.CreerContexte();
        var envoyes = await CreerRelais(db).RelayerAsync(50, tentativesMaximum: 10, CancellationToken.None);

        Assert.Equal(0, envoyes);
        await _publication.DidNotReceiveWithAnyArgs().Publish(default(object)!, default(Type)!, default(IPipe<PublishContext>)!, default);
    }
}
