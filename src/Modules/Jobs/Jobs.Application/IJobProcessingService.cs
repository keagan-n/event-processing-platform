namespace Jobs.Application;

public interface IJobProcessingService
{
    Task ProcessJobAsync(Guid jobId, CancellationToken cancellationToken);
}
