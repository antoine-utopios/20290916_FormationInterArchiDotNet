namespace Textinord.Trame.Domain.Catalogue;

/// <summary>
/// Un entrepôt Textinord. La priorité sert à choisir l'entrepôt qui sert une ligne
/// quand plusieurs ont du stock (Roubaix, le siège, avant Lesquin).
/// </summary>
public sealed record Entrepot(string Code, string Site, int Priorite)
{
    public static readonly Entrepot Roubaix = new("RBX", "Roubaix", 1);

    public static readonly Entrepot Lesquin = new("LSQ", "Lesquin", 2);

    public static IReadOnlyList<Entrepot> Tous { get; } = [Roubaix, Lesquin];
}
