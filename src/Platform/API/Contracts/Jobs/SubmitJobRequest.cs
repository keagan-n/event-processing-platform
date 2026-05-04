namespace Platform.Api.Contracts.Jobs;

public sealed record SubmitJobRequest(
    Guid TenantId,
    string JobType,
    string Payload);