namespace Textinord.Extranet.Modeles;

/// <summary>Client professionnel connecté à l'extranet et sa condition tarifaire.</summary>
public sealed record Client(string Code, string RaisonSociale, decimal RemiseClientPourcent)
{
    /// <summary>Client de démonstration tant que l'authentification Entra External ID n'est pas branchée (module 5).</summary>
    public static Client Demonstration { get; } = new("HOT-0421", "Hôtel Beaulieu Lille", 10m);
}
