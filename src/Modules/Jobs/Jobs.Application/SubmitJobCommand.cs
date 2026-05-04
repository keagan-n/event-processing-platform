namespace Jobs.Application;

public sealed record SubmitJobCommand(
    Guid TenantId,
    string JobType,
    string Payload);