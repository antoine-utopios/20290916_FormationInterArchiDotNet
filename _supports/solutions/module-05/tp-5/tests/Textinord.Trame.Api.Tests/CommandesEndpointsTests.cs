using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Textinord.Trame.Api.Commandes;
using Textinord.Trame.Api.Domaine;

namespace Textinord.Trame.Api.Tests;

public sealed class CommandesEndpointsTests(TrameApiFactory fabrique) : IClassFixture<TrameApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Liste_des_commandes_paginee_pour_un_lecteur()
    {
        using var client = fabrique.Entrepot();

        var reponse = await client.GetAsync("/api/v1/commandes?taille=2");
        var page = await reponse.Content.ReadFromJsonAsync<Page<CommandeResume>>(Json);

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.NotNull(page);
        Assert.Equal(2, page.Taille);
        Assert.Equal(2, page.Elements.Count);
        Assert.True(page.Total >= 3, "le jeu de données amorce au moins trois commandes");
        Assert.Contains("1.0, 2.0", reponse.Headers.GetValues("api-supported-versions").First());
    }

    [Fact]
    public async Task Filtre_par_statut_ne_retourne_que_ce_statut()
    {
        using var client = fabrique.Entrepot();

        var page = await client.GetFromJsonAsync<Page<CommandeResume>>("/api/v1/commandes?statut=EnAttenteStock", Json);

        Assert.NotNull(page);
        Assert.NotEmpty(page.Elements);
        Assert.All(page.Elements, c => Assert.Equal(StatutCommande.EnAttenteStock, c.Statut));
    }

    [Fact]
    public async Task Creation_valide_repond_201_avec_Location_et_numero_au_bon_format()
    {
        using var client = fabrique.Adv();
        var requete = new CreerCommandeRequete("CLI-0500",
        [
            new LigneRequete("VT-PANT-44", 600),
            new LigneRequete("HOT-SERV-50", 10, 5.00m),
        ]);

        var reponse = await client.PostAsJsonAsync("/api/v1/commandes", requete, Json);
        var detail = await reponse.Content.ReadFromJsonAsync<CommandeDetailV1>(Json);

        Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        Assert.NotNull(detail);
        Assert.Matches(@"^CMD-\d{4}-\d{6}$", detail.Numero);
        Assert.Equal($"/api/v1/commandes/{detail.Numero}", reponse.Headers.Location?.ToString());
        Assert.Equal(StatutCommande.Brouillon, detail.Statut);
        // Grand compte 25 % + volume 5 % sur 600 pièces = 30 % (plafond atteint) ; 10 pièces : 25 % seulement.
        Assert.Equal(0.30m, detail.Lignes[0].TauxRemise);
        Assert.Equal(0.25m, detail.Lignes[1].TauxRemise);
        Assert.Equal(600 * 29.50m * 0.70m + 10 * 5.00m * 0.75m, detail.TotalHT);
    }

    [Fact]
    public async Task Creation_invalide_repond_400_ValidationProblem_avec_les_champs_fautifs()
    {
        using var client = fabrique.Adv();
        var requete = new CreerCommandeRequete("", [new LigneRequete("", 0)]);

        var reponse = await client.PostAsJsonAsync("/api/v1/commandes", requete, Json);
        var probleme = await reponse.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json);

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Equal("application/problem+json", reponse.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(probleme);
        Assert.Contains("codeClient", probleme.Errors.Keys);
        Assert.Contains("lignes[0].reference", probleme.Errors.Keys);
        Assert.Contains("lignes[0].quantite", probleme.Errors.Keys);
    }

    [Fact]
    public async Task Client_inconnu_repond_422_avec_un_type_de_probleme_stable()
    {
        using var client = fabrique.Adv();
        var requete = new CreerCommandeRequete("CLI-9999", [new LigneRequete("VT-PANT-44", 1)]);

        var reponse = await client.PostAsJsonAsync("/api/v1/commandes", requete, Json);
        var probleme = await reponse.Content.ReadFromJsonAsync<ProblemDetails>(Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, reponse.StatusCode);
        Assert.NotNull(probleme);
        Assert.Equal("https://trame.textinord.example/problemes/client-inconnu", probleme.Type);
        Assert.Equal("CLI-9999", probleme.Extensions["codeClient"]?.ToString());
    }

    [Fact]
    public async Task Commande_introuvable_repond_404_ProblemDetails()
    {
        using var client = fabrique.Entrepot();

        var reponse = await client.GetAsync("/api/v1/commandes/CMD-2026-999999");
        var probleme = await reponse.Content.ReadFromJsonAsync<ProblemDetails>(Json);

        Assert.Equal(HttpStatusCode.NotFound, reponse.StatusCode);
        Assert.NotNull(probleme);
        Assert.Equal(404, probleme.Status);
        Assert.Equal("Commande introuvable", probleme.Title);
        Assert.Equal("https://trame.textinord.example/problemes/commande-introuvable", probleme.Type);
        Assert.True(probleme.Extensions.ContainsKey("traceId"));
    }

    [Fact]
    public async Task Validation_passe_en_Validee_puis_second_appel_repond_409()
    {
        using var client = fabrique.Adv();
        var creation = await client.PostAsJsonAsync("/api/v1/commandes",
            new CreerCommandeRequete("CLI-0042", [new LigneRequete("VT-PARKA-XL", 50)]), Json);
        var numero = (await creation.Content.ReadFromJsonAsync<CommandeDetailV1>(Json))!.Numero;

        var validation = await client.PostAsync($"/api/v1/commandes/{numero}/validation", null);
        var detail = await validation.Content.ReadFromJsonAsync<CommandeDetailV1>(Json);
        var seconde = await client.PostAsync($"/api/v1/commandes/{numero}/validation", null);
        var probleme = await seconde.Content.ReadFromJsonAsync<ProblemDetails>(Json);

        Assert.Equal(HttpStatusCode.OK, validation.StatusCode);
        Assert.Equal(StatutCommande.Validee, detail!.Statut);
        Assert.Equal("RBX", detail.Lignes[0].Entrepot);
        Assert.Equal(HttpStatusCode.Conflict, seconde.StatusCode);
        Assert.Equal("TRANSITION_INTERDITE", probleme!.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task Validation_sans_stock_passe_en_EnAttenteStock()
    {
        using var client = fabrique.Adv();
        var creation = await client.PostAsJsonAsync("/api/v1/commandes",
            new CreerCommandeRequete("CLI-0311", [new LigneRequete("EPI-GANT-09", 10)]), Json);
        var numero = (await creation.Content.ReadFromJsonAsync<CommandeDetailV1>(Json))!.Numero;

        var validation = await client.PostAsync($"/api/v1/commandes/{numero}/validation", null);
        var detail = await validation.Content.ReadFromJsonAsync<CommandeDetailV1>(Json);

        Assert.Equal(HttpStatusCode.OK, validation.StatusCode);
        Assert.Equal(StatutCommande.EnAttenteStock, detail!.Statut);
        Assert.Null(detail.Lignes[0].Entrepot);
    }

    [Fact]
    public async Task Annulation_repond_204_puis_404_apres_suppression_logique_impossible()
    {
        using var client = fabrique.Adv();
        var creation = await client.PostAsJsonAsync("/api/v1/commandes",
            new CreerCommandeRequete("CLI-0107", [new LigneRequete("HOT-SERV-50", 5)]), Json);
        var numero = (await creation.Content.ReadFromJsonAsync<CommandeDetailV1>(Json))!.Numero;

        var annulation = await client.DeleteAsync($"/api/v1/commandes/{numero}");
        var seconde = await client.DeleteAsync($"/api/v1/commandes/{numero}");
        var detail = await client.GetFromJsonAsync<CommandeDetailV1>($"/api/v1/commandes/{numero}", Json);

        Assert.Equal(HttpStatusCode.NoContent, annulation.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, seconde.StatusCode);
        Assert.Equal(StatutCommande.Annulee, detail!.Statut);
    }
}
