namespace Textinord.Trame.Domain;

public sealed class Article
{
    public required string Reference { get; init; }

    public required string Libelle { get; init; }

    public required string Famille { get; init; }

    public decimal PrixBase { get; init; }
}
