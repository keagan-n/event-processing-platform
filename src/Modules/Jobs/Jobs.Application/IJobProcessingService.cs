namespace Jobs.Application;

public interface IJobProcessingService
{
    Task<bool> ProcessNextPendingJobAsync(CancellationToken cancellationToken);
}