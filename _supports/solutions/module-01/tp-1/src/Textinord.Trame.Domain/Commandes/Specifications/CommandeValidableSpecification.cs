using Textinord.Trame.Domain.Communs;

namespace Textinord.Trame.Domain.Commandes.Specifications;

/// <summary>
/// Règle : une commande se valide seulement si elle a des lignes et si chaque ligne a du stock
/// dans au moins un entrepôt. Composée à partir de <see cref="StockDisponibleSpecification"/>.
/// </summary>
public sealed class CommandeValidableSpecification : Specification<Commande>
{
    private readonly Specification<LigneCommande> _ligneServable = new StockDisponibleSpecification();

    public override bool EstSatisfaitePar(Commande candidat) =>
        candidat.Lignes.Count > 0 && candidat.Lignes.All(_ligneServable.EstSatisfaitePar);

    /// <summary>Les lignes qui bloquent la validation (pour expliquer le passage en attente de stock).</summary>
    public IReadOnlyList<LigneCommande> LignesSansStock(Commande commande) =>
        commande.Lignes.Where(_ligneServable.Non().EstSatisfaitePar).ToList();
}
