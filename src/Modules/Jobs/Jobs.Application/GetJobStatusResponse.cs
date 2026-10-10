namespace Platform.Api.Contracts.Jobs;

public sealed record GetJobStatusResponse(
    Guid JobId,
    string TenantId,
    string JobType,
    string Status,
    DateTime CreatedUtc,
    string? Result,
    int AttemptCount,
    string? FailureReason);
