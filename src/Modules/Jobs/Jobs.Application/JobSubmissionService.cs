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
        var errors = Validate(command);

        if (errors.Count > 0)
        {
            return SubmitJobResult.Failure(errors);
        }

        var job = Job.Create(
            tenantId: command.TenantId,
            jobType: command.JobType,
            payload: command.Payload);

        await _jobRepository.AddAsync(job, cancellationToken);
        await _jobRepository.SaveChangesAsync(cancellationToken);

        return SubmitJobResult.Success(
            jobId: job.Id,
            tenantId: job.TenantId,
            jobType: job.JobType,
            status: job.Status.ToString(),
            createdUtc: job.CreatedUtc);
    }

    private static List<string> Validate(SubmitJobCommand command)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(command.TenantId))
        {
            errors.Add("TenantId is required.");
        }

        if (string.IsNullOrWhiteSpace(command.JobType))
        {
            errors.Add("JobType is required.");
        }

        if (string.IsNullOrWhiteSpace(command.Payload))
        {
            errors.Add("Payload is required.");
        }

        return errors;
    }
}
