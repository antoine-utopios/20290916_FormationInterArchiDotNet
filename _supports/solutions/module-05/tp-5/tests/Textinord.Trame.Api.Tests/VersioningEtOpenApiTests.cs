using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Textinord.Trame.Api.Commandes;

namespace Textinord.Trame.Api.Tests;

public sealed class VersioningEtOpenApiTests(TrameApiFactory fabrique) : IClassFixture<TrameApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task La_v2_explique_la_remise_et_decompose_les_totaux()
    {
        using var client = fabrique.Adv();
        var creation = await client.PostAsJsonAsync("/api/v2/commandes",
            new CreerCommandeRequete("CLI-0500", [new LigneRequete("EPI-GILET-L", 1000)]), Json);
        var numero = (await creation.Content.ReadFromJsonAsync<CommandeDetailV1>(Json))!.Numero;

        var v1 = await client.GetFromJsonAsync<JsonElement>($"/api/v1/commandes/{numero}", Json);
        var v2 = await client.GetFromJsonAsync<CommandeDetailV2>($"/api/v2/commandes/{numero}", Json);

        Assert.Equal($"/api/v2/commandes/{numero}", creation.Headers.Location?.ToString());
        Assert.False(v1.TryGetProperty("totaux", out _), "la v1 ne connaît pas les totaux décomposés");
        Assert.NotNull(v2);
        Assert.True(v2.Lignes[0].Remise.PlafondAtteint);
        Assert.Equal(0.25m, v2.Lignes[0].Remise.Client);
        Assert.Equal(0.05m, v2.Lignes[0].Remise.Volume);
        Assert.Equal(0.30m, v2.Lignes[0].Remise.Appliquee);
        Assert.Equal(1000 * 4.20m, v2.Totaux.Brut);
        Assert.Equal(v2.Totaux.Brut - v2.Totaux.Remises, v2.Totaux.Net);
    }

    [Fact]
    public async Task Une_version_inconnue_dans_l_URL_est_une_ressource_inexistante_404_ProblemDetails()
    {
        using var client = fabrique.Adv();

        // Versionnée par segment d'URL, la v3 n'existe pas : 404 (avec un en-tête ou un query string,
        // Asp.Versioning répondrait 400 « Unsupported API version »). Dans les deux cas, un ProblemDetails.
        var reponse = await client.GetAsync("/api/v3/commandes");
        var probleme = await reponse.Content.ReadFromJsonAsync<ProblemDetails>(Json);

        Assert.Equal(HttpStatusCode.NotFound, reponse.StatusCode);
        Assert.Equal("application/problem+json", reponse.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(probleme);
        Assert.Equal(404, probleme.Status);
    }

    [Theory]
    [InlineData("v1", "/api/v1/commandes")]
    [InlineData("v2", "/api/v2/commandes/{numero}")]
    public async Task Le_document_OpenAPI_de_chaque_version_est_publie(string document, string chemin)
    {
        using var client = fabrique.Anonyme();

        var reponse = await client.GetAsync($"/openapi/{document}.json");
        var contenu = await reponse.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(contenu);

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal("Trame 2 — API commandes", json.RootElement.GetProperty("info").GetProperty("title").GetString());
        Assert.True(json.RootElement.GetProperty("paths").TryGetProperty(chemin, out _), $"chemin {chemin} absent du document {document}");
        Assert.True(json.RootElement.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _));
    }

    [Fact]
    public async Task Scalar_est_servi()
    {
        using var client = fabrique.Anonyme();

        var reponse = await client.GetAsync("/scalar");

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Contains("text/html", reponse.Content.Headers.ContentType?.MediaType);
    }
}
