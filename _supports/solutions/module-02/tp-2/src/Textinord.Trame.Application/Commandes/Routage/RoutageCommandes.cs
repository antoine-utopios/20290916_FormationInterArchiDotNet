using Textinord.Trame.Application.Commandes.Messages;
using Textinord.Trame.Application.Messaging;

namespace Textinord.Trame.Application.Commandes.Routage;

/// <summary>
/// Règles de routage des commandes validées : France vers le flux national,
/// tout autre pays vers le flux export ; un pays absent est une donnée invalide.
/// </summary>
public static class RoutageCommandes
{
    public static ContentBasedRouter CreerRouteur() =>
        new ContentBasedRouter()
            .Rejeter<CommandeValidee>("pays de livraison absent", c => string.IsNullOrWhiteSpace(c.PaysLivraison))
            .Rejeter<CommandeValidee>("commande sans ligne", c => c.Lignes.Count == 0)
            .Quand<CommandeValidee>("France", c => !c.EstExport, CanauxTrame.PreparationsFrance)
            .Quand<CommandeValidee>("Export", c => c.EstExport, CanauxTrame.PreparationsExport);
}
