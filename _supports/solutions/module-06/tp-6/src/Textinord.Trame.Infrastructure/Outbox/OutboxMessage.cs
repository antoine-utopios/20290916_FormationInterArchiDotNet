namespace Textinord.Trame.Infrastructure.Outbox;

/// <summary>
/// Ligne de la table Outbox : le message est écrit dans la même transaction que la donnée
/// métier, puis publié par le relais du Worker. Tant que EnvoyeLe est null, il reste à publier.
/// </summary>
public sealed class OutboxMessage
{
    public long Id { get; init; }

    /// <summary>Identifiant stable du message : réutilisé comme MessageId sur le bus.</summary>
    public Guid MessageId { get; init; } = Guid.NewGuid();

    /// <summary>Nom court du type (voir <see cref="OutboxSerialiseur"/>).</summary>
    public required string Type { get; init; }

    /// <summary>Corps JSON du message.</summary>
    public required string Contenu { get; init; }

    public DateTimeOffset CreeLe { get; init; }

    public DateTimeOffset? EnvoyeLe { get; set; }

    public int Tentatives { get; set; }

    public string? DerniereErreur { get; set; }
}
