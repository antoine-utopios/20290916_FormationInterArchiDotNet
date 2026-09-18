using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public class MessageReceiver
{
    private readonly string _hostName;
    private IConnection? _connection;
    private IChannel? _channel;

    public MessageReceiver(string hostName)
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

    public async Task StartListeningAsync(Action<CustomMessage> onMessageReceived)
    {
        if (_channel is null)
        {
            throw new InvalidOperationException("On doit se connecter et avoir le canal avant de chercher à envoyer!");
        }

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, message) =>
        {
            var json = Encoding.UTF8.GetString(message.Body.ToArray());
            var newMessage = JsonSerializer.Deserialize<CustomMessage>(json);

            if (newMessage is not null) onMessageReceived(newMessage);

            await _channel.BasicAckAsync(message.DeliveryTag, multiple: false);
        };

        await _channel.BasicConsumeAsync(queue: "messages", autoAck: false, consumer: consumer);
    }
}

public record CustomMessage(string author, string Text);