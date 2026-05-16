namespace Jobs.Domain;

public sealed class Job
{
    public Guid Id { get; private set; }
    public string TenantId { get; private set; }
    public string JobType { get; private set; }
    public string Payload { get; private set; }
    public JobStatus Status { get; private set; }
    public DateTime CreatedUtc { get; private set; }

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
            tenantId: tenantId,
            jobType: jobType.Trim(),
            payload: payload,
            status: JobStatus.Pending,
            createdUtc: DateTime.UtcNow);
    }

    public void MarkQueued()
    {
        if (Status != JobStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot move job from {Status} to Queued.");
        }

        Status = JobStatus.Queued;
    }
}