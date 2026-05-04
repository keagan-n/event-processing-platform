namespace Jobs.Application;

using Jobs.Domain;

public sealed class JobSubmissionService : IJobSubmissionService
{
    public Task<SubmitJobResult> SubmitAsync(
        SubmitJobCommand command,
        CancellationToken cancellationToken)
    {
        var job = Job.Create(
            tenantId: command.TenantId,
            jobType: command.JobType,
            payload: command.Payload);

        var result = new SubmitJobResult(
            JobId: job.Id,
            TenantId: job.TenantId,
            JobType: job.JobType,
            Status: job.Status.ToString(),
            CreatedUtc: job.CreatedUtc);

        return Task.FromResult(result);
    }
}