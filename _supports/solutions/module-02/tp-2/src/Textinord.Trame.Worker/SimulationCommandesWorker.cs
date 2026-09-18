using Textinord.Trame.Application.Commandes;
using Textinord.Trame.Application.Commandes.Messages;
using Textinord.Trame.Application.Messaging;
using Textinord.Trame.Domain.Commandes;

namespace Textinord.Trame.Worker;

/// <summary>
/// Simule une matinée d'ADV : valide les commandes du jeu de démonstration, injecte un message
/// invalide pour montrer la dead letter, affiche le bilan, puis arrête l'hôte.
/// </summary>
public sealed partial class SimulationCommandesWorker(
    IServiceScopeFactory scopes,
    ICommandeRepository commandes,
    IMessageBus bus,
    DeadLetterChannel deadLetters,
    HistoriqueMessages historique,
    TimeProvider horloge,
    IHostApplicationLifetime vie,
    ILogger<SimulationCommandesWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Laisse le bus démarrer (les hosted services démarrent dans l'ordre d'enregistrement).
        await Task.Delay(TimeSpan.FromMilliseconds(200), stoppingToken).ConfigureAwait(false);

        var correlations = new List<Guid>();

        foreach (var commande in await commandes.ListerAsync(stoppingToken).ConfigureAwait(false))
        {
            using var scope = scopes.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<ValiderCommandeHandler>();
            var resultat = await handler.ExecuterAsync(new ValiderCommande(commande.Numero.Valeur), stoppingToken).ConfigureAwait(false);

            JournaliserValidation(resultat.Numero, resultat.Statut, resultat.CorrelationId);

            if (resultat.CorrelationId is { } correlation)
            {
                correlations.Add(correlation);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(300), stoppingToken).ConfigureAwait(false);
        }

        // Un message malformé venu d'un producteur externe : le routeur le rejette.
        var invalide = new CommandeValidee("CMD-2026-999999", "CLI-000001", PaysLivraison: null, Urgente: false, Lignes: []);
        await bus.PublierAsync(CanauxTrame.CommandesValidees, invalide, cancellationToken: stoppingToken).ConfigureAwait(false);

        // Un entrepôt inconnu : le traducteur refuse, rien ne part.
        var entrepotInconnu = new CommandeValidee("CMD-2026-999998", "CLI-000001", "FR", Urgente: false,
            Lignes: [new LigneValidee("VT-BLOUSE-M", 10, "PARIS")]);
        await bus.PublierAsync(CanauxTrame.CommandesValidees, entrepotInconnu, cancellationToken: stoppingToken).ConfigureAwait(false);

        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);

        foreach (var correlation in correlations)
        {
            var etapes = historique.ParCorrelation(correlation)
                .Select(m => $"{m.Canal} ← {m.Message.Type}{(m.Message.NumeroSequence is { } n ? $" ({n}/{m.Message.TailleSequence})" : string.Empty)}");
            var resume = string.Join(" | ", etapes);
            JournaliserHistorique(correlation, resume);
        }

        var maintenant = horloge.GetUtcNow();
        var nombreDeadLetters = deadLetters.Nombre;
        JournaliserBilan(maintenant, correlations.Count, nombreDeadLetters);

        foreach (var lettre in deadLetters.Lettres)
        {
            JournaliserDeadLetter(lettre.Message.Type, lettre.Canal, lettre.Raison);
        }

        vie.StopApplication();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Commande {Numero} : {Statut} (corrélation {CorrelationId})")]
    private partial void JournaliserValidation(string numero, StatutCommande? statut, Guid? correlationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Historique {CorrelationId} : {Etapes}")]
    private partial void JournaliserHistorique(Guid correlationId, string etapes);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bilan à {Maintenant:HH:mm:ss} : {NombreValidees} commande(s) validée(s), {NombreDeadLetters} dead letter(s)")]
    private partial void JournaliserBilan(DateTimeOffset maintenant, int nombreValidees, int nombreDeadLetters);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dead letter : {Type} depuis « {Canal} » : {Raison}")]
    private partial void JournaliserDeadLetter(string type, string canal, string raison);
}
