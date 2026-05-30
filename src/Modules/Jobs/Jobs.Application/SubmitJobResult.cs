namespace Jobs.Application;

public sealed record SubmitJobResult(
    bool Succeeded,
    IReadOnlyCollection<string> Errors,
    Guid JobId,
    string TenantId,
    string JobType,
    string Status,
    DateTime CreatedUtc)
{
    public static SubmitJobResult Success(
        Guid jobId,
        string tenantId,
        string jobType,
        string status,
        DateTime createdUtc)
    {
        return new SubmitJobResult(
            Succeeded: true,
            Errors: Array.Empty<string>(),
            JobId: jobId,
            TenantId: tenantId,
            JobType: jobType,
            Status: status,
            CreatedUtc: createdUtc);
    }

    public static SubmitJobResult Failure(IReadOnlyCollection<string> errors)
    {
        return new SubmitJobResult(
            Succeeded: false,
            Errors: errors,
            JobId: Guid.Empty,
            TenantId: string.Empty,
            JobType: string.Empty,
            Status: string.Empty,
            CreatedUtc: default);
    }
}
