using Textinord.Trame.Application.Commandes.Messages;
using Textinord.Trame.Application.Messaging;
using Textinord.Trame.Domain.Entrepots;

namespace Textinord.Trame.Application.Commandes.Traduction;

/// <summary>
/// Message Translator : du format canonique <see cref="CommandeValidee"/> vers le format
/// du WMS d'entrepôt. Pur, sans état, testable sans bus.
/// </summary>
public sealed class OrdrePreparationTranslator : IMessageTranslator<CommandeParEntrepot, OrdrePreparationEntrepot>
{
    public OrdrePreparationEntrepot Traduire(CommandeParEntrepot source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var entrepot = Entrepot.ParCode(source.EntrepotCode)
            ?? throw new TraductionException(
                $"Entrepôt inconnu « {source.EntrepotCode} » sur la commande {source.Commande.Numero}.");

        var lignes = source.Commande.Lignes
            .Where(ligne => string.Equals(ligne.EntrepotCode, entrepot.Code, StringComparison.OrdinalIgnoreCase))
            .Select((ligne, index) => new LignePreparation(index + 1, ligne.Reference.ToUpperInvariant(), ligne.Quantite))
            .ToList();

        if (lignes.Count == 0)
        {
            throw new TraductionException(
                $"Aucune ligne de la commande {source.Commande.Numero} ne concerne l'entrepôt {entrepot.Code}.");
        }

        return new OrdrePreparationEntrepot(
            NumeroOrdre: $"OP-{source.Commande.Numero[4..]}-{entrepot.Code}",
            Site: entrepot.Site.ToUpperInvariant(),
            NumeroCommande: source.Commande.Numero,
            CodeClient: source.Commande.ClientCode.ToUpperInvariant(),
            TypeFlux: source.Commande.EstExport ? "EXPORT" : "FRANCE",
            Priorite: source.Commande.Urgente ? "URGENTE" : "NORMALE",
            Lignes: lignes);
    }
}
