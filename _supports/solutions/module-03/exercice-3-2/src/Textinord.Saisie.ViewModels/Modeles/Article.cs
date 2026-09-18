namespace Textinord.Saisie.ViewModels.Modeles;

public sealed record Article(string Reference, string Libelle, decimal PrixBase)
{
    public override string ToString() => $"{Reference} - {Libelle}";
}
