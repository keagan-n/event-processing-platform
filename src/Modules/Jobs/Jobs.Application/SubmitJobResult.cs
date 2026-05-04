namespace Jobs.Application;

public sealed record SubmitJobResult(
    Guid JobId,
    Guid TenantId,
    string JobType,
    string Status,
    DateTime CreatedUtc);