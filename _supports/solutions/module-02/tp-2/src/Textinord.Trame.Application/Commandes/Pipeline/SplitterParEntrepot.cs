using Textinord.Trame.Application.Commandes.Messages;
using Textinord.Trame.Application.Messaging;

namespace Textinord.Trame.Application.Commandes.Pipeline;

/// <summary>
/// Splitter + Message Translator : une commande validée devient un ordre de préparation
/// par entrepôt concerné, numéroté dans une séquence (1/n … n/n) pour que l'aval sache
/// quand la commande est complète.
/// </summary>
public sealed class SplitterParEntrepot(IMessageTranslator<CommandeParEntrepot, OrdrePreparationEntrepot> traducteur)
{
    public async Task TraiterAsync(MessageEnvelope message, IMessageContext contexte, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(contexte);

        var commande = message.CorpsEnTantQue<CommandeValidee>();
        var entrepots = commande.EntrepotsConcernes;

        // Traduire d'abord, publier ensuite : si un entrepôt est inconnu, rien n'est parti.
        var ordres = entrepots
            .Select(code => (Code: code, Ordre: traducteur.Traduire(new CommandeParEntrepot(commande, code))))
            .ToList();

        for (var index = 0; index < ordres.Count; index++)
        {
            var (code, ordre) = ordres[index];
            var enveloppe = message.Deriver(ordre, contexte.Maintenant).AvecSequence(index + 1, ordres.Count);
            await contexte.PublierEnveloppeAsync(CanauxTrame.Entrepot(code), enveloppe, cancellationToken).ConfigureAwait(false);
        }
    }
}
