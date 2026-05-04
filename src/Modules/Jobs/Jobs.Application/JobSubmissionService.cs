using Jobs.Domain;

namespace Jobs.Application;

public sealed class JobSubmissionService : IJobSubmissionService
{
    private readonly IJobRepository _jobRepository;

    public JobSubmissionService(IJobRepository jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public async Task<SubmitJobResult> SubmitAsync(
        SubmitJobCommand command,
        CancellationToken cancellationToken)
    {
        var job = Job.Create(
            tenantId: command.TenantId,
            jobType: command.JobType,
            payload: command.Payload);

        await _jobRepository.AddAsync(job, cancellationToken);
        await _jobRepository.SaveChangesAsync(cancellationToken);

        return new SubmitJobResult(
            JobId: job.Id,
            TenantId: job.TenantId,
            JobType: job.JobType,
            Status: job.Status.ToString(),
            CreatedUtc: job.CreatedUtc);
    }
}