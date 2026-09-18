using Textinord.Trame.Domain.Entrepots;

namespace Textinord.Trame.Application.Commandes;

/// <summary>
/// Noms des canaux du flux de commande. Convention : « domaine.sujet », en minuscules,
/// stables dans le temps car ils deviendront des topics / files Azure Service Bus au module 6.
/// </summary>
public static class CanauxTrame
{
    public const string CommandesValidees = "commandes.validees";

    public const string PreparationsFrance = "preparations.france";

    public const string PreparationsExport = "preparations.export";

    public const string Audit = "audit";

    public static string Entrepot(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return $"entrepot.{code.ToUpperInvariant()}";
    }

    public static string Entrepot(Entrepot entrepot)
    {
        ArgumentNullException.ThrowIfNull(entrepot);
        return Entrepot(entrepot.Code);
    }
}
