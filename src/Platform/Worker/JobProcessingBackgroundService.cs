using Jobs.Application;

namespace Platform.Worker;

public sealed class JobProcessingBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<JobProcessingBackgroundService> _logger;

    public JobProcessingBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<JobProcessingBackgroundService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job processing worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var processingService = scope.ServiceProvider.GetRequiredService<IJobProcessingService>();

                var processed = await processingService.ProcessNextPendingJobAsync(stoppingToken);

                if (processed)
                {
                    _logger.LogInformation("Processed one pending job.");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error while processing pending jobs.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }

        _logger.LogInformation("Job processing worker stopped.");
    }
}
