using Jobs.Contracts;
using Jobs.Domain;
using System.Text;

namespace Jobs.Application;

public sealed class JobSubmissionService : IJobSubmissionService
{
    private readonly IJobRepository _jobRepository;
    private readonly IOutboxWriter _outboxWriter;

    public JobSubmissionService(
        IJobRepository jobRepository,
        IOutboxWriter outboxWriter)
    {
        _jobRepository = jobRepository;
        _outboxWriter = outboxWriter;
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

        var tenantId = command.TenantId.Trim();
        var jobType = command.JobType.Trim();
        var payload = command.Payload.Trim();

        var job = Job.Create(
            tenantId: tenantId,
            jobType: jobType,
            payload: payload);

        await _jobRepository.AddAsync(job, cancellationToken);

        // Transactional outbox: the message row is staged in the same unit of
        // work, so job + outbox row commit atomically. The dispatcher
        // publishes it to RabbitMQ afterwards.
        await _outboxWriter.EnqueueAsync(
            new JobSubmittedMessage(job.Id),
            cancellationToken);

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
        else if (command.TenantId.Trim().Length > 255)
        {
            errors.Add("TenantId must be at most 255 characters.");
        }

        if (string.IsNullOrWhiteSpace(command.JobType))
        {
            errors.Add("JobType is required.");
        }
        else if (command.JobType.Trim().Length > 255)
        {
            errors.Add("JobType must be at most 255 characters.");
        }

        if (string.IsNullOrWhiteSpace(command.Payload))
        {
            errors.Add("Payload is required.");
        }
        else if (Encoding.UTF8.GetByteCount(command.Payload.Trim()) > 64 * 1024)
        {
            errors.Add("Payload must be at most 65536 UTF-8 bytes.");
        }

        return errors;
    }
}
