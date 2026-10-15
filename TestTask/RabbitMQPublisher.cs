using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace TestTask;

public class RabbitMQPublisher
{
    private readonly IConnection connection;
    private readonly IChannel channel;

    public RabbitMQPublisher(IConnection connection)
    {
        this.connection = connection;
        this.channel = connection.CreateChannelAsync().Result;
    }

    public async Task PublishAsync(string message, string exchangeName, string routingKey)
    {
        await channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Direct);

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties()
        {
            ContentType = "application/json"
        };

        await channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body
        );
    }

    public async Task CloseConnection()
    {
        await connection.CloseAsync();
    }
}