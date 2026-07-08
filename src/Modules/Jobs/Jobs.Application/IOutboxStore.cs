namespace Jobs.Application;

public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxItem>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken);

    Task MarkProcessedAsync(Guid outboxMessageId, CancellationToken cancellationToken);

    Task MarkFailedAsync(Guid outboxMessageId, string error, CancellationToken cancellationToken);
}
