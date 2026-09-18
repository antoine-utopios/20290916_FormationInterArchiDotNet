namespace Textinord.Trame.Application.Messaging;

/// <summary>
/// Enveloppe d'un message (pattern Envelope Wrapper) : le corps métier plus les en-têtes
/// qui font vivre le message dans le système — identité, corrélation, causalité,
/// expiration, adresse de retour, position dans une séquence, indicateur de format.
/// </summary>
public sealed record MessageEnvelope
{
    public required Guid MessageId { get; init; }

    /// <summary>Identifiant partagé par tous les messages d'un même flux métier (Correlation Identifier).</summary>
    public required Guid CorrelationId { get; init; }

    /// <summary>Identifiant du message qui a provoqué celui-ci (Message History minimale).</summary>
    public Guid? CausationId { get; init; }

    /// <summary>Nom du type de corps et version de format (Format Indicator), ex. « CommandeValidee.v1 ».</summary>
    public required string Type { get; init; }

    public required object Corps { get; init; }

    public required DateTimeOffset EmisLe { get; init; }

    /// <summary>Message Expiration : au-delà, le message part en dead letter sans être traité.</summary>
    public DateTimeOffset? ExpireLe { get; init; }

    /// <summary>Return Address : canal sur lequel une réponse est attendue, s'il y a lieu.</summary>
    public string? AdresseRetour { get; init; }

    /// <summary>Message Sequence : position (1..n) et taille de la séquence.</summary>
    public int? NumeroSequence { get; init; }

    public int? TailleSequence { get; init; }

    public static MessageEnvelope Creer<T>(
        T corps,
        DateTimeOffset emisLe,
        Guid? correlationId = null,
        TimeSpan? dureeDeVie = null,
        string? adresseRetour = null)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(corps);

        return new MessageEnvelope
        {
            MessageId = Guid.CreateVersion7(emisLe),
            CorrelationId = correlationId ?? Guid.CreateVersion7(emisLe),
            Type = NomDeType<T>(),
            Corps = corps,
            EmisLe = emisLe,
            ExpireLe = dureeDeVie is { } duree ? emisLe + duree : null,
            AdresseRetour = adresseRetour,
        };
    }

    /// <summary>
    /// Crée un message dérivé : même corrélation, causation = ce message, même adresse de retour,
    /// même date limite. C'est ainsi que la corrélation se propage sans effort dans un pipeline.
    /// </summary>
    public MessageEnvelope Deriver<T>(T corps, DateTimeOffset emisLe)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(corps);

        return new MessageEnvelope
        {
            MessageId = Guid.CreateVersion7(emisLe),
            CorrelationId = CorrelationId,
            CausationId = MessageId,
            Type = NomDeType<T>(),
            Corps = corps,
            EmisLe = emisLe,
            ExpireLe = ExpireLe,
            AdresseRetour = AdresseRetour,
        };
    }

    public MessageEnvelope AvecSequence(int numero, int taille)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(numero);
        ArgumentOutOfRangeException.ThrowIfLessThan(taille, numero);

        return this with { NumeroSequence = numero, TailleSequence = taille };
    }

    public bool EstExpire(DateTimeOffset maintenant) => ExpireLe is { } limite && maintenant > limite;

    public bool EstDeType<T>() => Corps is T;

    public T CorpsEnTantQue<T>() =>
        Corps is T corps
            ? corps
            : throw new InvalidOperationException(
                $"Le message {MessageId} est de type {Type}, pas {typeof(T).Name}.");

    public static string NomDeType<T>() => $"{typeof(T).Name}.v1";
}
