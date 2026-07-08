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
        Publish(message, _options.QueueName, headers: null, cancellationToken);

        return Task.CompletedTask;
    }

    public Task PublishJobRetryAsync(
        JobSubmittedMessage message,
        CancellationToken cancellationToken)
    {
        // Attempt state lives on the job row (PostgreSQL is the system of
        // record), so a retry is a plain republish of the same message.
        Publish(message, _options.QueueName, headers: null, cancellationToken);

        return Task.CompletedTask;
    }

    public Task PublishJobFailedAsync(
        JobSubmittedMessage message,
        string failureReason,
        CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, object>
        {
            ["x-failure-reason"] = failureReason
        };

        Publish(message, _options.DeadLetterQueueName, headers, cancellationToken);

        return Task.CompletedTask;
    }

    private void Publish(
        JobSubmittedMessage message,
        string queueName,
        IDictionary<string, object>? headers,
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
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var json = JsonSerializer.Serialize(new { jobId = message.JobId });
        var body = Encoding.UTF8.GetBytes(json);

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;

        if (headers is not null)
        {
            properties.Headers = headers;
        }

        channel.BasicPublish(
            exchange: string.Empty,
            routingKey: queueName,
            basicProperties: properties,
            body: body);
    }
}
