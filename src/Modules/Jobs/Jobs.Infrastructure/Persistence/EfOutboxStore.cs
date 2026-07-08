using Jobs.Application;
using Microsoft.EntityFrameworkCore;

namespace Jobs.Infrastructure.Persistence;

public sealed class EfOutboxStore : IOutboxStore
{
    private readonly JobsDbContext _dbContext;

    public EfOutboxStore(JobsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<OutboxItem>> GetUnprocessedAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        return await _dbContext.OutboxMessages
            .Where(message => message.ProcessedUtc == null)
            .OrderBy(message => message.OccurredUtc)
            .Take(batchSize)
            .Select(message => new OutboxItem(message.Id, message.Type, message.Payload))
            .ToListAsync(cancellationToken);
    }

    public async Task MarkProcessedAsync(Guid outboxMessageId, CancellationToken cancellationToken)
    {
        var message = await _dbContext.OutboxMessages.FindAsync(
            new object[] { outboxMessageId },
            cancellationToken);

        if (message is null)
        {
            return;
        }

        message.ProcessedUtc = DateTime.UtcNow;
        message.Error = null;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(Guid outboxMessageId, string error, CancellationToken cancellationToken)
    {
        var message = await _dbContext.OutboxMessages.FindAsync(
            new object[] { outboxMessageId },
            cancellationToken);

        if (message is null)
        {
            return;
        }

        message.Error = error;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
