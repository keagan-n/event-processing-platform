using Jobs.Application;
using Jobs.Domain;
using Microsoft.EntityFrameworkCore;

namespace Jobs.Infrastructure.Persistence;

public sealed class JobRepository : IJobRepository
{
    private readonly JobsDbContext _dbContext;

    public JobRepository(JobsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Job job, CancellationToken cancellationToken)
    {
        await _dbContext.Jobs.AddAsync(job, cancellationToken);
    }

    public async Task<Job?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken)
    {
        return await _dbContext.Jobs
            .FirstOrDefaultAsync(job => job.Id == jobId, cancellationToken);
    }

    public async Task<Job?> GetNextPendingAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Jobs
            .Where(job => job.Status == JobStatus.Pending)
            .OrderBy(job => job.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}