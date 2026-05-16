namespace Jobs.Application;

public sealed record SubmitJobResult(
    Guid JobId,
    string TenantId,
    string JobType,
    string Status,
    DateTime CreatedUtc);