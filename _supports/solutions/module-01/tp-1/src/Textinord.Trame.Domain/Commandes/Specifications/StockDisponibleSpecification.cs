using Textinord.Trame.Domain.Communs;

namespace Textinord.Trame.Domain.Commandes.Specifications;

/// <summary>
/// Règle : une ligne est servable si au moins un entrepôt a un stock suffisant pour sa quantité.
/// </summary>
public sealed class StockDisponibleSpecification : Specification<LigneCommande>
{
    public override bool EstSatisfaitePar(LigneCommande candidat) =>
        candidat.Article.EntrepotPouvantServir(candidat.Quantite) is not null;
}
