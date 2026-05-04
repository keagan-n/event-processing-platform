namespace Jobs.Application;

public sealed record GetJobStatusResult(
    Guid JobId,
    Guid TenantId,
    string JobType,
    string Status,
    DateTime CreatedUtc);