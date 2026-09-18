using System.Reflection;
using Textinord.Trame.Api.Commandes;
using Textinord.Trame.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTrameInfrastructure(builder.Configuration);
builder.Services.AddScoped<PriseDeCommande>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();
app.MapCommandes();
app.MapGet("/", () => TypedResults.Redirect("/openapi/v1.json")).ExcludeFromDescription();

// Version SemVer calculée par Nerdbank.GitVersioning et estampillée dans l'assembly.
app.MapGet("/version", () => TypedResults.Ok(new
{
    Version = typeof(Program).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "inconnue",
    Produit = "Trame 2",
})).WithTags("Diagnostic").WithName("Version");

app.Run();

// Rend la classe visible de WebApplicationFactory<Program> dans les tests d'intégration.
public partial class Program;
