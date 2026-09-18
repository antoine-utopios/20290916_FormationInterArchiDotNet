using Microsoft.EntityFrameworkCore;
using Textinord.Trame.Domain;
using Textinord.Trame.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Textinord.Trame.Infrastructure.Tests;

public sealed class ConcurrenceOptimisteTests : BaseSqliteEnMemoire
{
    private async Task<int> PreparerCommandeBrouillonAsync()
    {
        var (client, article1, _) = JeuDeDonnees();
        await using var contexte = CreerContexte();
        contexte.AddRange(client, article1);
        await contexte.SaveChangesAsync();

        var commande = new Commande("CMD-2026-000100", client, DateTime.UtcNow);
        commande.AjouterLigne(article1, 20);
        contexte.Add(commande);
        await contexte.SaveChangesAsync();
        return commande.Id;
    }

    [Fact]
    public async Task Modifier_une_commande_renouvelle_son_jeton_de_version()
    {
        var id = await PreparerCommandeBrouillonAsync();

        await using var contexte = CreerContexte();
        var commande = await new CommandeRepository(contexte).ObtenirAsync(id);
        var versionAvant = commande!.Version;

        commande.Valider(_ => true);
        await contexte.SauvegarderAsync();

        Assert.NotEqual(versionAvant, commande.Version);
    }

    [Fact]
    public async Task Modifier_les_metadonnees_json_renouvelle_aussi_le_jeton()
    {
        var id = await PreparerCommandeBrouillonAsync();

        await using var contexte = CreerContexte();
        var commande = await contexte.Commandes.SingleAsync(c => c.Id == id);
        var versionAvant = commande.Version;

        commande.Metadonnees.Etiqueter("urgent");
        await contexte.SaveChangesAsync();

        Assert.NotEqual(versionAvant, commande.Version);
    }

    [Fact]
    public async Task Deux_modifications_concurrentes_la_seconde_echoue_avec_DbUpdateConcurrencyException()
    {
        var id = await PreparerCommandeBrouillonAsync();

        // Sofia (ADV) et le client (extranet) ouvrent la même commande au même moment.
        await using var contexteSofia = CreerContexte();
        await using var contexteExtranet = CreerContexte();
        var commandeSofia = await new CommandeRepository(contexteSofia).ObtenirAsync(id);
        var commandeExtranet = await new CommandeRepository(contexteExtranet).ObtenirAsync(id);
        Assert.Equal(commandeSofia!.Version, commandeExtranet!.Version);

        // Sofia valide en premier : UPDATE ... WHERE Version = ancienne → 1 ligne.
        commandeSofia.Valider(_ => true);
        await contexteSofia.SauvegarderAsync();

        // Le client annule sur une version périmée : UPDATE ... WHERE Version = ancienne → 0 ligne.
        commandeExtranet.Annuler("erreur de quantité");
        var exception = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => contexteExtranet.SauvegarderAsync());

        // L'exception porte la commande et son owned type JSON (l'annulation modifie aussi le commentaire).
        var entreeCommande = Assert.Single(exception.Entries, e => e.Entity is Commande);

        // Résolution « la base a raison » : recharger, puis rejouer la décision métier sur l'état réel.
        foreach (var entree in exception.Entries)
        {
            await entree.ReloadAsync();
        }
        var rechargee = (Commande)entreeCommande.Entity;
        Assert.Equal(StatutCommande.Validee, rechargee.Statut);
        Assert.Equal(commandeSofia.Version, rechargee.Version);
    }
}
