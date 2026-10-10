namespace Jobs.Application;

using System.Text.Json;
using Jobs.Domain;
using Microsoft.Extensions.Logging;

public sealed class JobProcessingService : IJobProcessingService
{
    private readonly IJobRepository _jobRepository;
    private readonly IAiAnalysisClient _aiAnalysisClient;
    private readonly JobProcessingOptions _options;
    private readonly ILogger<JobProcessingService> _logger;

    public JobProcessingService(
        IJobRepository jobRepository,
        IAiAnalysisClient aiAnalysisClient,
        JobProcessingOptions options,
        ILogger<JobProcessingService> logger)
    {
        _jobRepository = jobRepository;
        _aiAnalysisClient = aiAnalysisClient;
        _options = options;
        _logger = logger;
    }

    public async Task<JobProcessingResult> ProcessJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken);

        if (job is null)
        {
            throw new InvalidOperationException($"Job {jobId} was not found.");
        }

        // Idempotency guard: delivery is at-least-once, so a redelivered
        // message for a job that already reached a terminal (or in-flight)
        // state must be a no-op.
        if (job.Status != JobStatus.Pending)
        {
            return JobProcessingResult.Skipped();
        }

        job.MarkProcessing();
        await _jobRepository.SaveChangesAsync(cancellationToken);

        AiAnalysisResult analysisResult;

        try
        {
            if (ShouldForceFailure(job.Payload))
            {
                throw new InvalidOperationException("Forced failure requested via payload.");
            }

            var analysisRequest = new AiAnalysisRequest(
                JobId: job.Id,
                JobType: job.JobType,
                Payload: job.Payload);

            analysisResult = await _aiAnalysisClient.AnalyzeAsync(
                analysisRequest,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(analysisResult.Summary))
            {
                throw new InvalidOperationException("AI service returned an empty summary.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (job.AttemptCount + 1 < _options.MaxAttempts)
            {
                job.MarkPendingForRetry();
                await _jobRepository.SaveChangesAsync(cancellationToken);

                _logger.LogWarning(
                    "Job attempt failed; retry scheduled. JobId: {JobId}, TenantId: {TenantId}, AttemptCount: {AttemptCount}, Reason: {FailureReason}",
                    job.Id,
                    job.TenantId,
                    job.AttemptCount,
                    exception.Message);

                return JobProcessingResult.RetryScheduled(exception.Message);
            }

            job.MarkFailed(exception.Message);
            await _jobRepository.SaveChangesAsync(cancellationToken);

            _logger.LogError(
                "Job failed permanently. JobId: {JobId}, TenantId: {TenantId}, AttemptCount: {AttemptCount}, Reason: {FailureReason}",
                job.Id,
                job.TenantId,
                job.AttemptCount,
                exception.Message);

            return JobProcessingResult.FailedPermanently(exception.Message);
        }

        // Persistence failures are infrastructure failures, not AI failures.
        // Do not try to transition a Completed entity back to Pending/Failed
        // when the completion save throws; preserve the original exception.
        job.MarkCompleted(analysisResult.Summary);
        await _jobRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Job completed. JobId: {JobId}, TenantId: {TenantId}, AttemptCount: {AttemptCount}, Model: {Model}",
            job.Id,
            job.TenantId,
            job.AttemptCount,
            analysisResult.Model);

        return JobProcessingResult.Completed();
    }

    private static bool ShouldForceFailure(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);

            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("forceFailure", out var forceFailureElement))
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
