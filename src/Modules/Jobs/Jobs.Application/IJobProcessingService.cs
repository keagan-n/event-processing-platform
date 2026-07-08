namespace Jobs.Application;

public interface IJobProcessingService
{
    Task<JobProcessingResult> ProcessJobAsync(Guid jobId, CancellationToken cancellationToken);
}
