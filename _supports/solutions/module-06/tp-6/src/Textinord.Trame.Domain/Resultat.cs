namespace Textinord.Trame.Domain;

/// <summary>
/// Pattern Result : une opération métier renvoie soit une valeur, soit une liste
/// d'erreurs lisibles par l'utilisateur. Pas d'exception pour les cas attendus.
/// </summary>
public sealed class Resultat<T>
{
    private Resultat(T? valeur, IReadOnlyList<string> erreurs)
    {
        Valeur = valeur;
        Erreurs = erreurs;
    }

    public T? Valeur { get; }

    public IReadOnlyList<string> Erreurs { get; }

    public bool EstSucces => Erreurs.Count == 0;

    public static Resultat<T> Succes(T valeur) => new(valeur, []);

    public static Resultat<T> Echec(params string[] erreurs) => new(default, erreurs);
}
