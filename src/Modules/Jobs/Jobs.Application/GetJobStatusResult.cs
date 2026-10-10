namespace Jobs.Application;

public sealed record GetJobStatusResult(
    Guid JobId,
    string TenantId,
    string JobType,
    string Status,
    DateTime CreatedUtc,
    string? Result,
    int AttemptCount,
    string? FailureReason);
