namespace Textinord.Trame.Domain.Communs;

/// <summary>
/// Pattern Specification : une règle métier nommée, testable seule, composable avec Et / Ou / Non.
/// </summary>
public abstract class Specification<T>
{
    public abstract bool EstSatisfaitePar(T candidat);

    public Specification<T> Et(Specification<T> autre) => new SpecificationEt<T>(this, autre);

    public Specification<T> Ou(Specification<T> autre) => new SpecificationOu<T>(this, autre);

    public Specification<T> Non() => new SpecificationNon<T>(this);

    /// <summary>Construit une spécification ad hoc à partir d'un prédicat (utile dans les tests).</summary>
    public static Specification<T> Depuis(Func<T, bool> predicat) => new SpecificationPredicat<T>(predicat);
}

internal sealed class SpecificationEt<T>(Specification<T> gauche, Specification<T> droite) : Specification<T>
{
    public override bool EstSatisfaitePar(T candidat) =>
        gauche.EstSatisfaitePar(candidat) && droite.EstSatisfaitePar(candidat);
}

internal sealed class SpecificationOu<T>(Specification<T> gauche, Specification<T> droite) : Specification<T>
{
    public override bool EstSatisfaitePar(T candidat) =>
        gauche.EstSatisfaitePar(candidat) || droite.EstSatisfaitePar(candidat);
}

internal sealed class SpecificationNon<T>(Specification<T> interne) : Specification<T>
{
    public override bool EstSatisfaitePar(T candidat) => !interne.EstSatisfaitePar(candidat);
}

internal sealed class SpecificationPredicat<T>(Func<T, bool> predicat) : Specification<T>
{
    public override bool EstSatisfaitePar(T candidat) => predicat(candidat);
}
