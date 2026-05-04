using Jobs.Domain;

namespace Jobs.Application;

public interface IJobRepository
{
    Task AddAsync(Job job, CancellationToken cancellationToken);

    Task<Job?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken);

    Task<Job?> GetNextPendingAsync(CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}