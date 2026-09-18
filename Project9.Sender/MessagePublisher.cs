using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

public class MessagePublisher
{
    private readonly string _hostName;
    private IConnection? _connection;
    private IChannel? _channel;

    public MessagePublisher(string hostName)
    {
        _hostName = hostName;
    }

    public async Task ConnectAsync()
    {
        var factory = new ConnectionFactory { HostName = _hostName };
        _connection = await factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        await _channel.QueueDeclareAsync(
            queue: "messages",
            durable: false,
            exclusive: false,
            autoDelete: false
        );
    }

    public async Task SendAsync(string author, string text)
    {
        if (_channel is null)
        {
            throw new InvalidOperationException("On doit se connecter et avoir le canal avant de chercher à envoyer!");
        }

        var newMessage = new CustomMessage(author, text);
        var json = JsonSerializer.Serialize(newMessage);
        var bytes = Encoding.UTF8.GetBytes(json);

        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: "messages",
            body: bytes
        );
    }

}

public record CustomMessage(string author, string Text);