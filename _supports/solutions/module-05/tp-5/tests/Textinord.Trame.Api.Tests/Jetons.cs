using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;

namespace Textinord.Trame.Api.Tests;

/// <summary>
/// Fabrique des jetons JWT signés avec la clé de test, équivalents à ceux de `dotnet user-jwts create --role ...`.
/// </summary>
public static class Jetons
{
    public static string Creer(string utilisateur, params string[] roles)
    {
        var cle = new SymmetricSecurityKey(Convert.FromBase64String(TrameApiFactory.CleBase64));
        var signature = new SigningCredentials(cle, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim> { new("sub", utilisateur) };
        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var jeton = new JwtSecurityToken(
            issuer: TrameApiFactory.Issuer,
            audience: TrameApiFactory.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: signature);

        return new JwtSecurityTokenHandler().WriteToken(jeton);
    }

    public static HttpClient Adv(this WebApplicationFactory<Program> fabrique, string utilisateur = "sofia.marques") =>
        fabrique.ClientAvec(Creer(utilisateur, "adv"));

    public static HttpClient Entrepot(this WebApplicationFactory<Program> fabrique, string utilisateur = "marc.vandewalle") =>
        fabrique.ClientAvec(Creer(utilisateur, "entrepot"));

    public static HttpClient Anonyme(this WebApplicationFactory<Program> fabrique) => fabrique.CreateClient();

    private static HttpClient ClientAvec(this WebApplicationFactory<Program> fabrique, string jeton)
    {
        var client = fabrique.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jeton);
        return client;
    }
}
