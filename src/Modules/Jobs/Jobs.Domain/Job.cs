namespace Jobs.Domain;

public sealed class Job
{
    public Guid Id { get; private set; }
    public string TenantId { get; private set; }
    public string JobType { get; private set; }
    public string Payload { get; private set; }
    public JobStatus Status { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime? StartedUtc { get; private set; }
    public DateTime? CompletedUtc { get; private set; }
    public DateTime? FailedUtc { get; private set; }
    public string? FailureReason { get; private set; }
    public string? Result { get; private set; }
    public int AttemptCount { get; private set; }

    private Job(
        Guid id,
        string tenantId,
        string jobType,
        string payload,
        JobStatus status,
        DateTime createdUtc)
    {
        Id = id;
        TenantId = tenantId;
        JobType = jobType;
        Payload = payload;
        Status = status;
        CreatedUtc = createdUtc;
    }

    private Job()
    {
        TenantId = string.Empty;
        JobType = string.Empty;
        Payload = string.Empty;
    }

    public static Job Create(string tenantId, string jobType, string payload)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(jobType))
        {
            throw new ArgumentException("JobType is required.", nameof(jobType));
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("Payload is required.", nameof(payload));
        }

        return new Job(
            id: Guid.NewGuid(),
            tenantId: tenantId.Trim(),
            jobType: jobType.Trim(),
            payload: payload,
            status: JobStatus.Pending,
            createdUtc: DateTime.UtcNow);
    }

    public void MarkProcessing()
    {
        if (Status != JobStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot move job from {Status} to Processing.");
        }

        Status = JobStatus.Processing;
        StartedUtc = DateTime.UtcNow;
    }

    public void MarkCompleted(string result)
    {
        if (Status != JobStatus.Processing)
        {
            throw new InvalidOperationException($"Cannot move job from {Status} to Completed.");
        }

        if (string.IsNullOrWhiteSpace(result))
        {
            throw new ArgumentException("Result is required.", nameof(result));
        }

        Status = JobStatus.Completed;
        CompletedUtc = DateTime.UtcNow;
        Result = result.Trim();
    }

    public void MarkPendingForRetry()
    {
        if (Status != JobStatus.Processing)
        {
            throw new InvalidOperationException($"Cannot move job from {Status} back to Pending for retry.");
        }

        Status = JobStatus.Pending;
        AttemptCount++;
    }

    public void MarkFailed(string failureReason)
    {
        if (Status != JobStatus.Processing)
        {
            throw new InvalidOperationException($"Cannot move job from {Status} to Failed.");
        }

        if (string.IsNullOrWhiteSpace(failureReason))
        {
            throw new ArgumentException("Failure reason is required.", nameof(failureReason));
        }

        Status = JobStatus.Failed;
        FailedUtc = DateTime.UtcNow;
        FailureReason = failureReason.Trim();
    }
}