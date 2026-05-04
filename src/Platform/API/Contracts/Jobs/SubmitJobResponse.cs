namespace Platform.Api.Contracts.Jobs;

public sealed record SubmitJobResponse(
    Guid JobId,
    Guid TenantId,
    string JobType,
    string Status,
    DateTime CreatedUtc);