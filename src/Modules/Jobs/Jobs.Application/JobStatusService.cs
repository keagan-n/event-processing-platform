namespace Jobs.Application;

public sealed class JobStatusService : IJobStatusService
{
    private readonly IJobRepository _jobRepository;

    public JobStatusService(IJobRepository jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public async Task<GetJobStatusResult?> GetStatusAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken);

        if (job is null)
        {
            return null;
        }

        return new GetJobStatusResult(
            JobId: job.Id,
            TenantId: job.TenantId,
            JobType: job.JobType,
            Status: job.Status.ToString(),
            CreatedUtc: job.CreatedUtc,
            Result: job.Result,
            AttemptCount: job.AttemptCount,
            FailureReason: job.FailureReason);
    }
}
