using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Textinord.Trame.Domain;
using Textinord.Trame.Infrastructure;

namespace Textinord.Trame.Api.Commandes;

public static class CommandesEndpoints
{
    public static IEndpointRouteBuilder MapCommandes(this IEndpointRouteBuilder app)
    {
        var commandes = app.MapGroup("/commandes").WithTags("Commandes");

        commandes.MapPost("/", async Task<Results<Created<CommandeReponse>, ValidationProblem>> (
            NouvelleCommandeRequete requete, PriseDeCommande priseDeCommande, CancellationToken ct) =>
        {
            var resultat = await priseDeCommande.EnregistrerAsync(requete, ct);
            if (!resultat.EstSucces)
            {
                return TypedResults.ValidationProblem(
                    new Dictionary<string, string[]> { ["commande"] = [.. resultat.Erreurs] });
            }

            var commande = resultat.Valeur!;
            return TypedResults.Created($"/commandes/{commande.Numero}", VersReponse(commande));
        })
        .WithName("CreerCommande")
        .WithSummary("Enregistre une commande et, si le stock le permet, la valide (message Outbox).");

        commandes.MapGet("/{numero}", async Task<Results<Ok<CommandeReponse>, NotFound>> (
            string numero, TrameDbContext db, CancellationToken ct) =>
        {
            var commande = await db.Commandes
                .AsNoTracking()
                .Include(c => c.Lignes)
                .SingleOrDefaultAsync(c => c.Numero == numero, ct);

            return commande is null ? TypedResults.NotFound() : TypedResults.Ok(VersReponse(commande));
        })
        .WithName("LireCommande");

        commandes.MapGet("/{numero}/ordres-preparation", async (string numero, TrameDbContext db, CancellationToken ct) =>
        {
            var ordres = await db.OrdresPreparation
                .AsNoTracking()
                .Where(o => o.NumeroCommande == numero)
                .OrderBy(o => o.Entrepot)
                .Select(o => new OrdrePreparationReponse(
                    o.Id, o.Entrepot, o.Statut.ToString(), o.NombreLignes, o.NombrePieces, o.EmisLe, o.Preparateur))
                .ToListAsync(ct);

            return TypedResults.Ok(ordres);
        })
        .WithName("ListerOrdresPreparation");

        app.MapGet("/diagnostic/outbox", async (TrameDbContext db, CancellationToken ct) =>
        {
            var messages = await db.Outbox
                .AsNoTracking()
                .OrderBy(m => m.Id)
                .Select(m => new OutboxReponse(
                    m.Id, m.MessageId, m.Type, m.CreeLe, m.EnvoyeLe, m.Tentatives, m.DerniereErreur))
                .ToListAsync(ct);

            return TypedResults.Ok(messages);
        })
        .WithTags("Diagnostic")
        .WithName("ListerOutbox");

        return app;
    }

    private static CommandeReponse VersReponse(Commande commande) => new(
        commande.Numero,
        commande.CodeClient,
        commande.Statut.ToString(),
        commande.MontantNet,
        commande.CreeeLe,
        commande.ValideeLe,
        commande.Lignes
            .Select(l => new LigneReponse(
                l.ReferenceArticle, l.Quantite, l.PrixUnitaire, l.TauxRemise, l.MontantNet, l.EntrepotAffecte))
            .ToList());
}
