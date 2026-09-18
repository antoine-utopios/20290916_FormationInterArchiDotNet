namespace Textinord.Trame.Domain.Commandes;

/// <summary>Port du domaine : « existe-t-il au moins un entrepôt où cette quantité est disponible ? ».</summary>
public interface IDisponibiliteStock
{
    bool EstDisponible(string reference, int quantite);
}
