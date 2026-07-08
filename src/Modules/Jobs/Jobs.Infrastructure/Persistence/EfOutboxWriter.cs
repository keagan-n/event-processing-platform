using System.Text.Json;
using Jobs.Application;
using Jobs.Contracts;

namespace Jobs.Infrastructure.Persistence;

public sealed class EfOutboxWriter : IOutboxWriter
{
    private readonly JobsDbContext _dbContext;

    public EfOutboxWriter(JobsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task EnqueueAsync(JobSubmittedMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredUtc = DateTime.UtcNow,
            Type = nameof(JobSubmittedMessage),
            Payload = JsonSerializer.Serialize(message)
        };

        // Staged only — the caller's SaveChangesAsync commits this row and the
        // job in one transaction.
        _dbContext.OutboxMessages.Add(outboxMessage);

        return Task.CompletedTask;
    }
}
