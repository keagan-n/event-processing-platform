using System.Text;
using System.Text.Json;
using Jobs.Application;
using Jobs.Contracts;
using RabbitMQ.Client;

namespace Jobs.Infrastructure.Queue;

public sealed class RabbitMqJobQueuePublisher : IJobQueuePublisher
{
    private readonly RabbitMqOptions _options;

    public RabbitMqJobQueuePublisher(RabbitMqOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public Task PublishJobSubmittedAsync(
        JobSubmittedMessage message,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.Username,
            Password = _options.Password
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var json = JsonSerializer.Serialize(new { jobId = message.JobId });
        var body = Encoding.UTF8.GetBytes(json);

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;

        channel.BasicPublish(
            exchange: string.Empty,
            routingKey: _options.QueueName,
            basicProperties: properties,
            body: body);

        return Task.CompletedTask;
    }
}
