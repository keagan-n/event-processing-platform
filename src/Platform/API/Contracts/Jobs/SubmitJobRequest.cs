namespace Platform.Api.Contracts.Jobs;

public sealed record SubmitJobRequest(
    string TenantId,
    string JobType,
    string Payload);