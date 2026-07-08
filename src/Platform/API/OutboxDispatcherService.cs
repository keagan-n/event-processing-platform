using Jobs.Application;

namespace Platform.Api;

/// <summary>
/// In-process outbox dispatcher: polls the outbox table and publishes staged
/// messages to RabbitMQ. Runs inside the API host — a separate relay
/// deployable is intentionally out of scope.
/// </summary>
public sealed class OutboxDispatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxDispatcherService> _logger;

    public OutboxDispatcherService(
        IServiceScopeFactory serviceScopeFactory,
        OutboxOptions options,
        ILogger<OutboxDispatcherService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Outbox dispatcher started. Poll interval: {PollIntervalSeconds}s",
            _options.PollIntervalSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollIntervalSeconds));

        try
        {
            do
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();

                    var published = await processor.ProcessBatchAsync(stoppingToken);

                    if (published > 0)
                    {
                        _logger.LogInformation("Outbox dispatcher published {Count} message(s).", published);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // Failed rows stay unprocessed; the next tick retries them.
                    _logger.LogError(exception, "Outbox dispatch tick failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Outbox dispatcher stopping.");
        }
    }
}
