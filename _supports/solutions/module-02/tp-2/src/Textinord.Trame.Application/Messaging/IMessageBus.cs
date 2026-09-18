namespace Textinord.Trame.Application.Messaging;

/// <summary>
/// Contrat du bus de messages de Trame 2. L'implémentation in-process sert au développement
/// et aux tests ; le module 6 la remplace par MassTransit sur RabbitMQ / Azure Service Bus
/// sans toucher aux handlers.
/// </summary>
public interface IMessageBus
{
    IReadOnlyCollection<string> Canaux { get; }

    MessageChannel DeclarerCanal(string nom, int capacite = MessageChannel.CapaciteParDefaut);

    IMessageBus Abonner(string canal, MessageHandler handler);

    ValueTask PublierAsync<T>(
        string canal,
        T corps,
        Guid? correlationId = null,
        TimeSpan? dureeDeVie = null,
        CancellationToken cancellationToken = default)
        where T : notnull;

    ValueTask PublierEnveloppeAsync(string canal, MessageEnvelope message, CancellationToken cancellationToken = default);

    Task DemarrerAsync(CancellationToken cancellationToken = default);

    /// <summary>Ferme les canaux dans l'ordre de déclaration et attend que tout soit consommé.</summary>
    Task ArreterAsync(CancellationToken cancellationToken = default);
}
