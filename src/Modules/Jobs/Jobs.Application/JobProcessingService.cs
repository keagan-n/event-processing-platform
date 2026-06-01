namespace Jobs.Application;

using System.Text.Json;
using Jobs.Domain;

public sealed class JobProcessingService : IJobProcessingService
{
    private readonly IJobRepository _jobRepository;

    public JobProcessingService(IJobRepository jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public async Task ProcessJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken);

        if (job is null)
        {
            throw new InvalidOperationException($"Job {jobId} was not found.");
        }

        if (job.Status != JobStatus.Pending)
        {
            return;
        }

        job.MarkProcessing();
        await _jobRepository.SaveChangesAsync(cancellationToken);

        try
        {
            if (ShouldForceFailure(job.Payload))
            {
                throw new InvalidOperationException("Forced failure requested via payload.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

            job.MarkCompleted();
            await _jobRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            job.MarkFailed(exception.Message);
            await _jobRepository.SaveChangesAsync(cancellationToken);
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
