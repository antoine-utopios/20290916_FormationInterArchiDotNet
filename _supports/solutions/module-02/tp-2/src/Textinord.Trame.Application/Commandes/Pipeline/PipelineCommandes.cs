using Textinord.Trame.Application.Commandes.Messages;
using Textinord.Trame.Application.Commandes.Routage;
using Textinord.Trame.Application.Messaging;
using Textinord.Trame.Domain.Entrepots;

namespace Textinord.Trame.Application.Commandes.Pipeline;

/// <summary>
/// Câblage Pipes and Filters du flux de commande :
///
///   commandes.validees ─┬─ wire tap ──► audit ──► historique
///                       └─ routeur ──► preparations.france ─┐
///                                  └─► preparations.export ─┴─ splitter + traducteur ──► entrepot.RBX / entrepot.LSQ ──► gateway
///
/// Tout ce qui ne trouve pas sa route, expire ou échoue part sur la dead letter.
/// </summary>
public sealed class PipelineCommandes
{
    public PipelineCommandes(
        IMessageBus bus,
        IEntrepotGateway entrepots,
        HistoriqueMessages historique,
        IMessageTranslator<CommandeParEntrepot, OrdrePreparationEntrepot> traducteur)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(entrepots);
        ArgumentNullException.ThrowIfNull(historique);
        ArgumentNullException.ThrowIfNull(traducteur);

        Bus = bus;

        // Déclaration amont → aval : l'arrêt du bus vide les canaux dans cet ordre.
        bus.DeclarerCanal(CanauxTrame.CommandesValidees);
        bus.DeclarerCanal(CanauxTrame.PreparationsFrance);
        bus.DeclarerCanal(CanauxTrame.PreparationsExport);

        foreach (var entrepot in Entrepot.Tous)
        {
            bus.DeclarerCanal(CanauxTrame.Entrepot(entrepot));
        }

        bus.DeclarerCanal(CanauxTrame.Audit, capacite: HistoriqueMessages.CapaciteParDefaut);

        var routeur = RoutageCommandes.CreerRouteur();
        var splitter = new SplitterParEntrepot(traducteur);

        bus.Abonner(CanauxTrame.CommandesValidees, WireTap.Vers(CanauxTrame.Audit))
           .Abonner(CanauxTrame.CommandesValidees, routeur.RouterAsync)
           .Abonner(CanauxTrame.PreparationsFrance, splitter.TraiterAsync)
           .Abonner(CanauxTrame.PreparationsExport, splitter.TraiterAsync)
           .Abonner(CanauxTrame.Audit, historique.EnregistrerAsync);

        foreach (var entrepot in Entrepot.Tous)
        {
            bus.Abonner(CanauxTrame.Entrepot(entrepot), WireTap.Vers(CanauxTrame.Audit))
               .Abonner(CanauxTrame.Entrepot(entrepot), (message, _, cancellationToken) =>
                   entrepots.TransmettreAsync(entrepot, message.CorpsEnTantQue<OrdrePreparationEntrepot>(), cancellationToken));
        }
    }

    public IMessageBus Bus { get; }
}
