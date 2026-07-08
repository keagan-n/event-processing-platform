using Jobs.Contracts;

namespace Jobs.Application;

/// <summary>
/// Stages an outbound message in the SAME unit of work as the calling
/// service's entity changes, so both commit atomically on SaveChanges.
/// </summary>
public interface IOutboxWriter
{
    Task EnqueueAsync(JobSubmittedMessage message, CancellationToken cancellationToken);
}
