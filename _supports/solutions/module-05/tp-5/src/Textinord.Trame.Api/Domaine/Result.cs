namespace Textinord.Trame.Api.Domaine;

/// <summary>
/// Pattern Result (vu au TP 1) : une opération métier réussit ou échoue avec une <see cref="Erreur"/>.
/// Les exceptions restent réservées aux bugs et aux pannes techniques ; ici, un échec métier
/// devient un ProblemDetails 409 ou 422 dans la couche API, jamais un 500.
/// </summary>
public sealed class Result
{
    private Result(bool estSucces, Erreur? erreur)
    {
        EstSucces = estSucces;
        Erreur = erreur;
    }

    public bool EstSucces { get; }

    public bool EstEchec => !EstSucces;

    public Erreur? Erreur { get; }

    public static Result Ok() => new(true, null);

    public static Result Echec(string code, string message) => new(false, new Erreur(code, message));

    public override string ToString() => EstSucces ? "Succès" : $"Échec ({Erreur})";
}
