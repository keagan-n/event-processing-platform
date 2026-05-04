namespace Platform.Api.Contracts.Jobs;

public sealed record GetJobStatusResponse(
    Guid JobId,
    Guid TenantId,
    string JobType,
    string Status,
    DateTime CreatedUtc);