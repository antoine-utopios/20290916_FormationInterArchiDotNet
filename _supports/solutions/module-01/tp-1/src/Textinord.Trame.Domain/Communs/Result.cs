namespace Textinord.Trame.Domain.Communs;

/// <summary>
/// Pattern Result : une opération métier réussit ou échoue avec une <see cref="Erreur"/>.
/// On n'utilise pas les exceptions pour les cas métier prévisibles (stock insuffisant,
/// transition interdite) ; elles restent réservées aux bugs et aux pannes techniques.
/// </summary>
public class Result
{
    protected Result(bool estSucces, Erreur? erreur)
    {
        if (estSucces && erreur is not null)
        {
            throw new ArgumentException("Un succès ne porte pas d'erreur.", nameof(erreur));
        }

        if (!estSucces && erreur is null)
        {
            throw new ArgumentException("Un échec porte toujours une erreur.", nameof(erreur));
        }

        EstSucces = estSucces;
        Erreur = erreur;
    }

    public bool EstSucces { get; }

    public bool EstEchec => !EstSucces;

    public Erreur? Erreur { get; }

    public static Result Ok() => new(true, null);

    public static Result Echec(Erreur erreur) => new(false, erreur);

    public static Result Echec(string code, string message) => new(false, new Erreur(code, message));

    public static Result<T> Ok<T>(T valeur) => Result<T>.Ok(valeur);

    public static Result<T> Echec<T>(string code, string message) => Result<T>.Echec(code, message);

    public override string ToString() => EstSucces ? "Succès" : $"Échec ({Erreur})";
}

/// <summary>
/// Variante typée : porte une valeur en cas de succès.
/// </summary>
public sealed class Result<T> : Result
{
    private readonly T? _valeur;

    private Result(bool estSucces, T? valeur, Erreur? erreur)
        : base(estSucces, erreur)
    {
        _valeur = valeur;
    }

    /// <summary>La valeur du succès. Lever une exception ici est volontaire : lire la valeur d'un échec est un bug.</summary>
    public T Valeur => EstSucces
        ? _valeur!
        : throw new InvalidOperationException($"Aucune valeur : le résultat est un échec ({Erreur}).");

    public static Result<T> Ok(T valeur) => new(true, valeur, null);

    public static new Result<T> Echec(Erreur erreur) => new(false, default, erreur);

    public static new Result<T> Echec(string code, string message) => new(false, default, new Erreur(code, message));

    public TSortie Selon<TSortie>(Func<T, TSortie> siSucces, Func<Erreur, TSortie> siEchec) =>
        EstSucces ? siSucces(_valeur!) : siEchec(Erreur!);

    public override string ToString() => EstSucces ? $"Succès ({_valeur})" : base.ToString();
}
