using System.Text.Json;
using Textinord.Trame.Domain.Events;

namespace Textinord.Trame.Infrastructure.Outbox;

/// <summary>
/// Sérialise les événements vers l'Outbox et les relit. Le registre de types est explicite :
/// on ne fait jamais Type.GetType() sur une chaîne venue de la base.
/// </summary>
public static class OutboxSerialiseur
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, Type> TypesConnus = new(StringComparer.Ordinal)
    {
        [nameof(CommandeValidee)] = typeof(CommandeValidee),
    };

    public static OutboxMessage Emballer(object evenement, DateTimeOffset creeLe)
    {
        var type = evenement.GetType();
        if (!TypesConnus.ContainsKey(type.Name))
        {
            throw new NotSupportedException($"Type de message non enregistré dans l'Outbox : {type.Name}.");
        }

        return new OutboxMessage
        {
            Type = type.Name,
            Contenu = JsonSerializer.Serialize(evenement, type, Options),
            CreeLe = creeLe,
        };
    }

    public static (object Message, Type Type) Deballer(OutboxMessage message)
    {
        if (!TypesConnus.TryGetValue(message.Type, out var type))
        {
            throw new NotSupportedException($"Type de message Outbox inconnu : {message.Type}.");
        }

        var objet = JsonSerializer.Deserialize(message.Contenu, type, Options)
            ?? throw new InvalidOperationException($"Message Outbox {message.Id} illisible.");

        return (objet, type);
    }
}
