namespace Textinord.Extranet.Modeles;

/// <summary>Une page de résultats paginés.</summary>
public sealed record PageResultat<T>(IReadOnlyList<T> Elements, int Page, int TaillePage, int Total)
{
    public int NombrePages => Total == 0 ? 1 : (int)Math.Ceiling(Total / (double)TaillePage);

    public static PageResultat<T> Vide(int taillePage) => new([], 1, taillePage, 0);
}
