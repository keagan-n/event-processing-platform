namespace Jobs.Application;

public sealed record SubmitJobCommand(
    string TenantId,
    string JobType,
    string Payload);