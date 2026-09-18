namespace Textinord.Trame.Application.Messaging;

/// <summary>Ce qu'un handler peut faire pendant le traitement d'un message.</summary>
public interface IMessageContext
{
    MessageEnvelope Message { get; }

    string Canal { get; }

    DateTimeOffset Maintenant { get; }

    /// <summary>Publie un message dérivé : la corrélation et la causalité sont renseignées automatiquement.</summary>
    ValueTask PublierAsync<T>(string canal, T corps, CancellationToken cancellationToken = default)
        where T : notnull;

    /// <summary>Publie une enveloppe telle quelle (routeur, wire tap, séquence construite à la main).</summary>
    ValueTask PublierEnveloppeAsync(string canal, MessageEnvelope message, CancellationToken cancellationToken = default);

    /// <summary>Envoie le message courant en dead letter avec une raison lisible.</summary>
    ValueTask RejeterAsync(string raison, CancellationToken cancellationToken = default);
}
