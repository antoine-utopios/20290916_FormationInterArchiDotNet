using System.Globalization;
using Textinord.Extranet.Components;
using Textinord.Extranet.Modeles;
using Textinord.Extranet.Services;

// L'extranet est francophone : une seule culture pour les prix et les dates.
var culture = CultureInfo.GetCultureInfo("fr-FR");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Services applicatifs. Le catalogue et le carnet de commandes sont partagés
// (singleton) ; le panier vit le temps du circuit SignalR de l'utilisateur (scoped).
builder.Services.AddSingleton<ICatalogueService>(_ => new CatalogueEnMemoire());
builder.Services.AddSingleton<ICommandeService, CommandeEnMemoire>();
builder.Services.AddScoped(_ => new PanierService(Client.Demonstration));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
