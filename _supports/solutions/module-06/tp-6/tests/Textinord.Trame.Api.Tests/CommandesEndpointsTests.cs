using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Textinord.Trame.Api.Commandes;

namespace Textinord.Trame.Api.Tests;

public sealed class CommandesEndpointsTests(TrameApiFactory usine) : IClassFixture<TrameApiFactory>
{
    private readonly HttpClient _client = usine.CreateClient();

    private static object Requete(string codeClient, params (string Reference, int Quantite)[] lignes) => new
    {
        codeClient,
        lignes = lignes.Select(l => new { reference = l.Reference, quantite = l.Quantite }).ToArray(),
    };

    [Fact]
    public async Task Post_commande_valide_retourne_201_et_ecrit_le_message_outbox_dans_la_meme_base()
    {
        var reponse = await _client.PostAsJsonAsync("/commandes", Requete("C-0001", ("VT-1001", 120), ("LH-3300", 40)));

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        var commande = await reponse.Content.ReadFromJsonAsync<CommandeReponse>();
        Assert.NotNull(commande);
        Assert.Equal("Validee", commande.Statut);
        Assert.Equal($"/commandes/{commande.Numero}", reponse.Headers.Location?.ToString());
        Assert.All(commande.Lignes, l => Assert.Equal("RBX", l.Entrepot));

        await using var db = usine.CreerContexte();
        var enBase = await db.Commandes.Include(c => c.Lignes).SingleAsync(c => c.Numero == commande.Numero);
        Assert.Equal(2, enBase.Lignes.Count);

        var message = await db.Outbox.SingleAsync(m => m.Contenu.Contains(commande.Numero));
        Assert.Equal("CommandeValidee", message.Type);
        Assert.Null(message.EnvoyeLe);
    }

    [Fact]
    public async Task Post_commande_en_rupture_passe_en_attente_stock_sans_message_outbox()
    {
        // VT-1050 : stock nul dans les deux entrepôts.
        var reponse = await _client.PostAsJsonAsync("/commandes", Requete("C-0042", ("VT-1001", 10), ("VT-1050", 5)));

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        var commande = await reponse.Content.ReadFromJsonAsync<CommandeReponse>();
        Assert.NotNull(commande);
        Assert.Equal("EnAttenteStock", commande.Statut);
        Assert.Null(commande.ValideeLe);

        await using var db = usine.CreerContexte();
        Assert.False(await db.Outbox.AnyAsync(m => m.Contenu.Contains(commande.Numero)));
    }

    [Fact]
    public async Task Post_commande_applique_la_remise_plafonnee_a_30_pour_cent()
    {
        // C-0117 : 25 % de remise client ; 600 pièces : + 5 % volume ; plafond 30 %.
        var reponse = await _client.PostAsJsonAsync("/commandes", Requete("C-0117", ("VT-1001", 600)));

        var commande = await reponse.Content.ReadFromJsonAsync<CommandeReponse>();
        Assert.NotNull(commande);
        var ligne = Assert.Single(commande.Lignes);
        Assert.Equal(0.30m, ligne.TauxRemise);
        Assert.Equal(600 * 24.90m * 0.70m, ligne.MontantNet);
    }

    [Fact]
    public async Task Post_commande_sans_ligne_retourne_400_avec_problem_details()
    {
        var reponse = await _client.PostAsJsonAsync("/commandes", Requete("C-0001"));

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Equal("application/problem+json", reponse.Content.Headers.ContentType?.MediaType);
        var probleme = await reponse.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(probleme);
        Assert.Contains("au moins une ligne", probleme.Errors["commande"].Single());
    }

    [Fact]
    public async Task Post_commande_pour_un_client_inactif_retourne_400()
    {
        var reponse = await _client.PostAsJsonAsync("/commandes", Requete("C-0900", ("VT-1001", 1)));

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        var probleme = await reponse.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(probleme);
        Assert.Contains("inconnu ou inactif", probleme.Errors["commande"].Single());
    }

    [Fact]
    public async Task Get_commande_inconnue_retourne_404()
    {
        var reponse = await _client.GetAsync("/commandes/CMD-1999-000001");

        Assert.Equal(HttpStatusCode.NotFound, reponse.StatusCode);
    }

    [Fact]
    public async Task Get_commande_existante_retourne_ses_lignes()
    {
        var creation = await _client.PostAsJsonAsync("/commandes", Requete("C-0001", ("EPI-2040", 20)));
        var creee = await creation.Content.ReadFromJsonAsync<CommandeReponse>();
        Assert.NotNull(creee);

        var lecture = await _client.GetFromJsonAsync<CommandeReponse>($"/commandes/{creee.Numero}");

        Assert.NotNull(lecture);
        Assert.Equal(creee.Numero, lecture.Numero);
        var ligne = Assert.Single(lecture.Lignes);
        Assert.Equal("LSQ", ligne.Entrepot);
        Assert.Equal(0.10m, ligne.TauxRemise);
    }

    [Fact]
    public async Task Version_expose_le_numero_semver_calcule_par_nerdbank_gitversioning()
    {
        var reponse = await _client.GetFromJsonAsync<Dictionary<string, string>>("/version");

        Assert.NotNull(reponse);
        Assert.StartsWith("2.1.", reponse["version"]);
        Assert.Equal("Trame 2", reponse["produit"]);
    }

    [Fact]
    public async Task Diagnostic_outbox_liste_les_messages_en_attente()
    {
        await _client.PostAsJsonAsync("/commandes", Requete("C-0042", ("LH-3300", 100)));

        var messages = await _client.GetFromJsonAsync<List<OutboxReponse>>("/diagnostic/outbox");

        Assert.NotNull(messages);
        Assert.Contains(messages, m => m.Type == "CommandeValidee" && m.EnvoyeLe is null);
    }
}
