namespace Textinord.Trame.Domain.Communs;

/// <summary>
/// Une erreur métier : un code stable (pour les tests, les logs, l'API) et un message lisible.
/// </summary>
public sealed record Erreur(string Code, string Message)
{
    public override string ToString() => $"{Code} : {Message}";
}
