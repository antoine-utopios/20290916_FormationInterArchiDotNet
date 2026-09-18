using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Textinord.Trame.Domain.Events;
using Textinord.Trame.Infrastructure;
using Textinord.Trame.Infrastructure.Outbox;
using Textinord.Trame.Worker.Consumers;
using Textinord.Trame.Worker.Outbox;

namespace Textinord.Trame.Worker.Tests;

/// <summary>
/// Tests d'intégration avec le test harness MassTransit : un vrai bus en mémoire,
/// le vrai relais, le vrai consumer, une SQLite en mémoire.
/// </summary>
public sealed class BusEnMemoireTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Instant = new(2026, 9, 7, 16, 0, 0, TimeSpan.FromHours(2));

    private readonly SqliteEnMemoire _base = new();
    private ServiceProvider _fournisseur = null!;
    private ITestHarness _harness = null!;

    public async Task InitializeAsync()
    {
        _fournisseur = new ServiceCollection()
            .AddLogging()
            .AddSingleton(TimeProvider.System)
            .AddDbContext<TrameDbContext>(o => o.UseSqlite(_base.Connexion))
            .AddScoped<OutboxRelay>()
            .AddMassTransitTestHarness(bus =>
            {
                bus.SetKebabCaseEndpointNameFormatter();
                bus.AddConsumer<OrdrePreparationConsumer>();
            })
            .BuildServiceProvider(validateScopes: true);

        _harness = _fournisseur.GetRequiredService<ITestHarness>();
        _harness.TestInactivityTimeout = TimeSpan.FromSeconds(2);
        await _harness.Start();
    }

    public async Task DisposeAsync()
    {
        await _harness.Stop();
        await _fournisseur.DisposeAsync();
        _base.Dispose();
    }

    private static CommandeValidee Evenement(string numero) => new(
        Guid.NewGuid(), numero, "C-0117", Instant,
        [new LigneAPreparer("VT-1001", 600, "RBX"), new LigneAPreparer("EPI-2040", 40, "LSQ")]);

    [Fact]
    public async Task Le_relais_publie_l_outbox_et_le_consumer_cree_les_ordres()
    {
        var evenement = Evenement("CMD-2026-000020");
        await using (var db = _base.CreerContexte())
        {
            db.Outbox.Add(OutboxSerialiseur.Emballer(evenement, Instant));
            await db.SaveChangesAsync();
        }

        int envoyes;
        await using (var scope = _fournisseur.CreateAsyncScope())
        {
            var relais = scope.ServiceProvider.GetRequiredService<OutboxRelay>();
            envoyes = await relais.RelayerAsync(50, 10, CancellationToken.None);
        }

        Assert.Equal(1, envoyes);
        Assert.True(await _harness.Published.Any<CommandeValidee>());
        Assert.True(await _harness.Consumed.Any<CommandeValidee>());

        var consumerHarness = _harness.GetConsumerHarness<OrdrePreparationConsumer>();
        Assert.True(await consumerHarness.Consumed.Any<CommandeValidee>());

        await using var verification = _base.CreerContexte();
        var ordres = await verification.OrdresPreparation
            .Where(o => o.CommandeId == evenement.CommandeId)
            .OrderBy(o => o.Entrepot)
            .Select(o => o.Entrepot)
            .ToListAsync();
        Assert.Equal(["LSQ", "RBX"], ordres);

        var message = await verification.Outbox.SingleAsync();
        Assert.NotNull(message.EnvoyeLe);
    }

    [Fact]
    public async Task Le_meme_evenement_livre_deux_fois_ne_produit_qu_un_ordre_par_entrepot()
    {
        // Cas réel : le relais Outbox est tombé entre Publish et le marquage EnvoyeLe, puis a
        // republié. Le broker peut dédupliquer sur MessageId (Service Bus, et le transport en
        // mémoire le fait aussi) ; on simule ici le cas défavorable : deux identifiants distincts.
        var evenement = Evenement("CMD-2026-000021");

        await _harness.Bus.Publish(evenement, ctx => ctx.MessageId = Guid.NewGuid());
        await _harness.Bus.Publish(evenement, ctx => ctx.MessageId = Guid.NewGuid());

        Assert.True(await _harness.Consumed.Any<CommandeValidee>());
        await _harness.InactivityTask;

        Assert.Equal(2, _harness.Consumed.Select<CommandeValidee>().Count());

        await using var verification = _base.CreerContexte();
        Assert.Equal(2, await verification.OrdresPreparation.CountAsync(o => o.CommandeId == evenement.CommandeId));
    }
}
