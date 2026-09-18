using System.Threading.Channels;

namespace Textinord.Trame.Application.Messaging;

/// <summary>
/// Un canal nommé, borné, adossé à System.Threading.Channels.
/// Un seul lecteur (le bus), plusieurs rédacteurs (les producteurs et les handlers).
/// </summary>
public sealed class MessageChannel
{
    public const int CapaciteParDefaut = 1_000;

    private readonly Channel<MessageEnvelope> _canal;

    public MessageChannel(string nom, int capacite = CapaciteParDefaut)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nom);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacite);

        Nom = nom;
        Capacite = capacite;
        _canal = Channel.CreateBounded<MessageEnvelope>(new BoundedChannelOptions(capacite)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public string Nom { get; }

    public int Capacite { get; }

    public ChannelReader<MessageEnvelope> Lecteur => _canal.Reader;

    public ChannelWriter<MessageEnvelope> Redacteur => _canal.Writer;

    public int EnAttente => _canal.Reader.Count;
}
