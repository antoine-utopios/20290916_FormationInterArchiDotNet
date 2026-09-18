using Microsoft.AspNetCore.Http.HttpResults;
using Textinord.Trame.Application.Commandes;
using Textinord.Trame.Application.Commandes.Messages;
using Textinord.Trame.Application.DependencyInjection;
using Textinord.Trame.Application.Messaging;
using Textinord.Trame.Infrastructure.DependencyInjection;
using Textinord.Trame.Infrastructure.Entrepots;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddTrameApplication();
builder.Services.AddTrameInfrastructure();

var app = builder.Build();

app.MapOpenApi();

var commandes = app.MapGroup("/commandes").WithTags("Commandes");

commandes.MapGet("/", async (ICommandeRepository depot, CancellationToken cancellationToken) =>
{
    var liste = await depot.ListerAsync(cancellationToken);
    return TypedResults.Ok(liste.Select(c => new CommandeResume(
        c.Numero.Valeur, c.ClientCode, c.PaysLivraison, c.Statut.ToString(), c.Urgente, c.Lignes.Count, c.MontantNet)));
});

commandes.MapPost("/{numero}/validation", async Task<Results<Ok<ResultatValidation>, NotFound<string>, BadRequest<string>>> (
    string numero, ValiderCommandeHandler handler, CancellationToken cancellationToken) =>
{
    try
    {
        var resultat = await handler.ExecuterAsync(new ValiderCommande(numero), cancellationToken);
        return resultat.Introuvable
            ? TypedResults.NotFound($"Commande {numero} introuvable.")
            : TypedResults.Ok(resultat);
    }
    catch (ArgumentException ex)
    {
        return TypedResults.BadRequest(ex.Message);
    }
    catch (InvalidOperationException ex)
    {
        return TypedResults.BadRequest(ex.Message);
    }
});

// Channel Adapter d'entrée : un producteur externe (Trame historique, EDI) dépose directement
// un événement au format canonique. Utile pour observer le routeur, le traducteur et la dead letter.
app.MapPost("/messages/commandes-validees", async (CommandeValidee evenement, IMessageBus bus, TimeProvider horloge, CancellationToken cancellationToken) =>
{
    var enveloppe = MessageEnvelope.Creer(evenement, horloge.GetUtcNow(), dureeDeVie: ValiderCommandeHandler.DureeDeVieEvenement);
    await bus.PublierEnveloppeAsync(CanauxTrame.CommandesValidees, enveloppe, cancellationToken);
    return TypedResults.Accepted($"/audit/{enveloppe.CorrelationId}", new { enveloppe.MessageId, enveloppe.CorrelationId });
}).WithTags("Messages");

var supervision = app.MapGroup("/").WithTags("Supervision");

supervision.MapGet("/ordres", (JournalEntrepotGateway gateway) =>
    TypedResults.Ok(gateway.Ordres.Select(o => new { Entrepot = o.Entrepot.Code, o.Ordre, o.TransmisLe })));

supervision.MapGet("/dead-letters", (DeadLetterChannel deadLetters) =>
    TypedResults.Ok(deadLetters.Lettres.Select(l => new DeadLetterResume(
        l.Message.MessageId, l.Message.CorrelationId, l.Message.Type, l.Canal, l.Raison, l.DeposeeLe, l.Message.Corps))));

supervision.MapGet("/audit/{correlationId:guid}", (Guid correlationId, HistoriqueMessages historique) =>
    TypedResults.Ok(historique.ParCorrelation(correlationId).Select(m => new EtapeAudit(
        m.VuLe, m.Canal, m.Message.Type, m.Message.MessageId, m.Message.CausationId, m.Message.NumeroSequence, m.Message.TailleSequence))));

supervision.MapGet("/canaux", (IMessageBus bus) => TypedResults.Ok(bus.Canaux));

app.Run();

internal sealed record CommandeResume(string Numero, string Client, string Pays, string Statut, bool Urgente, int NombreLignes, decimal MontantNet);

internal sealed record DeadLetterResume(Guid MessageId, Guid CorrelationId, string Type, string Canal, string Raison, DateTimeOffset DeposeeLe, object Corps);

internal sealed record EtapeAudit(DateTimeOffset VuLe, string Canal, string Type, Guid MessageId, Guid? CausationId, int? Sequence, int? TailleSequence);
