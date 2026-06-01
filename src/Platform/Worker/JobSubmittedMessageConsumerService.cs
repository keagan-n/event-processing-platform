using System.Text;
using System.Text.Json;
using Jobs.Application;
using Jobs.Contracts;
using Jobs.Infrastructure.Queue;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Platform.Worker;

public sealed class JobSubmittedMessageConsumerService : BackgroundService
{
    private readonly RabbitMqOptions _options;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<JobSubmittedMessageConsumerService> _logger;

    public JobSubmittedMessageConsumerService(
        RabbitMqOptions options,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<JobSubmittedMessageConsumerService> logger)
    {
        _options = options;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RabbitMQ consumer started. Listening on queue: {QueueName}", _options.QueueName);

        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.Username,
            Password = _options.Password,
            DispatchConsumersAsync = true
        };

        var connection = factory.CreateConnection();
        var channel = connection.CreateModel();

        try
        {
            channel.QueueDeclare(
                queue: _options.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.Received += async (_, eventArgs) =>
            {
                var body = eventArgs.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);

                try
                {
                    var message = JsonSerializer.Deserialize<JobSubmittedMessage>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (message is null)
                    {
                        _logger.LogWarning("Received null message from queue. Nacking without requeue.");
                        channel.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: false);
                        return;
                    }

                    _logger.LogInformation("Received JobSubmittedMessage for JobId: {JobId}", message.JobId);

                    using var scope = _serviceScopeFactory.CreateScope();
                    var processingService = scope.ServiceProvider.GetRequiredService<IJobProcessingService>();

                    await processingService.ProcessJobAsync(message.JobId, stoppingToken);

                    channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
                    _logger.LogInformation("Successfully processed JobId: {JobId}", message.JobId);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("was not found"))
                {
                    _logger.LogWarning(ex, "Job not found. Nacking without requeue.");
                    channel.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process message. Nacking without requeue.");
                    channel.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: false);
                }
            };

            channel.BasicConsume(
                queue: _options.QueueName,
                autoAck: false,
                consumer: consumer);

            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("RabbitMQ consumer stopping.");
        }
        finally
        {
            channel.Dispose();
            connection.Dispose();
        }
    }
}
