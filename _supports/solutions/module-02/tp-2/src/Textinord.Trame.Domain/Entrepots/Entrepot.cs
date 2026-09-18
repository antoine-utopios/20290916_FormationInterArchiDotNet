namespace Textinord.Trame.Domain.Entrepots;

/// <summary>Les deux entrepôts Textinord. Le code court est celui que connaît le WMS historique.</summary>
public sealed record Entrepot(string Code, string Site)
{
    public static Entrepot Roubaix { get; } = new("RBX", "Roubaix");

    public static Entrepot Lesquin { get; } = new("LSQ", "Lesquin");

    public static IReadOnlyList<Entrepot> Tous { get; } = [Roubaix, Lesquin];

    public static Entrepot? ParCode(string code) =>
        Tous.FirstOrDefault(entrepot => string.Equals(entrepot.Code, code, StringComparison.OrdinalIgnoreCase));
}
