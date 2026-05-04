namespace Jobs.Application;

public interface IJobStatusService
{
    Task<GetJobStatusResult?> GetStatusAsync(
        Guid jobId,
        CancellationToken cancellationToken);
}