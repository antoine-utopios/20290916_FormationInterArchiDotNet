namespace Textinord.Trame.Api.Domaine;

/// <summary>
/// Une erreur métier : un code stable (repris tel quel dans les ProblemDetails de l'API)
/// et un message lisible par un humain.
/// </summary>
public sealed record Erreur(string Code, string Message)
{
    public override string ToString() => $"{Code} : {Message}";
}
