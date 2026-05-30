namespace Jobs.Application;

using System.Text.Json;

public sealed class JobProcessingService : IJobProcessingService
{
    private readonly IJobRepository _jobRepository;

    public JobProcessingService(IJobRepository jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public async Task<bool> ProcessNextPendingJobAsync(CancellationToken cancellationToken)
    {
        var job = await _jobRepository.GetNextPendingAsync(cancellationToken);

        if (job is null)
        {
            return false;
        }

        var markedProcessing = false;

        try
        {
            job.MarkProcessing();
            markedProcessing = true;
            await _jobRepository.SaveChangesAsync(cancellationToken);

            if (ShouldForceFailure(job.Payload))
            {
                throw new InvalidOperationException("Forced failure requested via payload.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

            job.MarkCompleted();
            await _jobRepository.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (Exception exception)
        {
            if (markedProcessing)
            {
                job.MarkFailed(exception.Message);
                await _jobRepository.SaveChangesAsync(cancellationToken);
            }

            throw;
        }
    }

    private static bool ShouldForceFailure(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);

            if (!document.RootElement.TryGetProperty("forceFailure", out var forceFailureElement))
            {
                return false;
            }

            return forceFailureElement.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
