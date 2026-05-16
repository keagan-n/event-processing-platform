namespace Platform.Api.Contracts.Jobs;

public sealed record SubmitJobResponse(
    Guid JobId,
    string TenantId,
    string JobType,
    string Status,
    DateTime CreatedUtc);