using System.Threading.RateLimiting;

namespace Platform.Api.RateLimiting;

/// <summary>
/// In-memory per-tenant token-bucket rate limiter. Applied inside the
/// POST /jobs endpoint (not the ASP.NET rate-limiting middleware) because
/// the partition key comes from the request body, which is only available
/// after model binding.
/// </summary>
public sealed class TenantRateLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter;

    public int WindowSeconds { get; }

    public TenantRateLimiter(int tokensPerWindow, int windowSeconds)
    {
        if (tokensPerWindow <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tokensPerWindow));
        }

        if (windowSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowSeconds));
        }

        WindowSeconds = windowSeconds;

        _limiter = PartitionedRateLimiter.Create<string, string>(tenantId =>
            RateLimitPartition.GetTokenBucketLimiter(tenantId, _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = tokensPerWindow,
                TokensPerPeriod = tokensPerWindow,
                ReplenishmentPeriod = TimeSpan.FromSeconds(windowSeconds),
                AutoReplenishment = true,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
    }

    public async ValueTask<bool> TryAcquireAsync(string tenantId, CancellationToken cancellationToken)
    {
        using var lease = await _limiter.AcquireAsync(tenantId, permitCount: 1, cancellationToken);

        return lease.IsAcquired;
    }

    public void Dispose()
    {
        _limiter.Dispose();
    }
}
