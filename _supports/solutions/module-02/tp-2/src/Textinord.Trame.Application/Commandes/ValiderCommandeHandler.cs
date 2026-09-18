using Textinord.Trame.Application.Commandes.Messages;
using Textinord.Trame.Application.Messaging;
using Textinord.Trame.Domain.Commandes;

namespace Textinord.Trame.Application.Commandes;

public sealed record ResultatValidation(string Numero, StatutCommande? Statut, Guid? CorrelationId)
{
    public bool Introuvable => Statut is null;

    public static ResultatValidation Introuvee(NumeroCommande numero) => new(numero.Valeur, null, null);
}

/// <summary>
/// Cas d'usage « valider une commande » : charge l'agrégat, applique la règle métier,
/// persiste, puis publie l'événement CommandeValidee sur le bus avec une durée de vie
/// de quatre heures (au-delà, un ordre de préparation n'a plus de sens pour l'entrepôt).
/// </summary>
public sealed class ValiderCommandeHandler(
    ICommandeRepository commandes,
    IDisponibiliteStock stock,
    IMessageBus bus,
    TimeProvider horloge)
{
    public static readonly TimeSpan DureeDeVieEvenement = TimeSpan.FromHours(4);

    public async Task<ResultatValidation> ExecuterAsync(ValiderCommande commande, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commande);

        var numero = new NumeroCommande(commande.Numero);
        var agregat = await commandes.TrouverAsync(numero, cancellationToken).ConfigureAwait(false);

        if (agregat is null)
        {
            return ResultatValidation.Introuvee(numero);
        }

        agregat.Valider(stock);
        await commandes.EnregistrerAsync(agregat, cancellationToken).ConfigureAwait(false);

        if (agregat.Statut != StatutCommande.Validee)
        {
            return new ResultatValidation(numero.Valeur, agregat.Statut, null);
        }

        var evenement = new CommandeValidee(
            agregat.Numero.Valeur,
            agregat.ClientCode,
            agregat.PaysLivraison,
            agregat.Urgente,
            agregat.Lignes.Select(l => new LigneValidee(l.Reference, l.Quantite, l.EntrepotCode)).ToList());

        var enveloppe = MessageEnvelope.Creer(evenement, horloge.GetUtcNow(), dureeDeVie: DureeDeVieEvenement);
        await bus.PublierEnveloppeAsync(CanauxTrame.CommandesValidees, enveloppe, cancellationToken).ConfigureAwait(false);

        return new ResultatValidation(numero.Valeur, agregat.Statut, enveloppe.CorrelationId);
    }
}
