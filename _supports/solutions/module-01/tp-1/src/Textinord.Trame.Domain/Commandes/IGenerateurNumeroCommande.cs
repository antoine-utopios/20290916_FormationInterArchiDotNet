namespace Textinord.Trame.Domain.Commandes;

/// <summary>
/// Port (au sens hexagonal) : le domaine décrit le besoin, l'infrastructure fournira
/// une implémentation sur Azure SQL (séquence par année). Ici, une version en mémoire pour les tests.
/// </summary>
public interface IGenerateurNumeroCommande
{
    NumeroCommande Suivant(int annee);
}
