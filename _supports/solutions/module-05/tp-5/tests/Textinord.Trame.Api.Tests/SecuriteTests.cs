using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Textinord.Trame.Api.Commandes;

namespace Textinord.Trame.Api.Tests;

public sealed class SecuriteTests(TrameApiFactory fabrique) : IClassFixture<TrameApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Sans_jeton_la_lecture_repond_401_avec_WWW_Authenticate()
    {
        using var client = fabrique.Anonyme();

        var reponse = await client.GetAsync("/api/v1/commandes");

        Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
        Assert.Contains("Bearer", reponse.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task Jeton_signe_avec_une_autre_cle_repond_401()
    {
        using var client = fabrique.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJwaXJhdGUifQ.invalide");

        var reponse = await client.GetAsync("/api/v1/commandes");

        Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
    }

    [Fact]
    public async Task Role_entrepot_peut_lire_mais_pas_creer_403()
    {
        using var client = fabrique.Entrepot();

        var lecture = await client.GetAsync("/api/v1/commandes");
        var creation = await client.PostAsJsonAsync("/api/v1/commandes",
            new CreerCommandeRequete("CLI-0042", [new LigneRequete("VT-PARKA-XL", 1)]), Json);
        var probleme = await creation.Content.ReadFromJsonAsync<ProblemDetails>(Json);

        Assert.Equal(HttpStatusCode.OK, lecture.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, creation.StatusCode);
        Assert.NotNull(probleme);
        Assert.Equal(403, probleme.Status);
    }

    [Fact]
    public async Task Le_referentiel_des_statuts_est_public()
    {
        using var client = fabrique.Anonyme();

        var reponse = await client.GetAsync("/api/v1/referentiel/statuts");
        var statuts = await reponse.Content.ReadFromJsonAsync<List<StatutDescription>>(Json);

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.NotNull(statuts);
        Assert.Equal(7, statuts.Count);
        Assert.Contains(statuts, s => s.Libelle == "En attente de stock");
    }
}
