namespace Platform.Api.Tests;

using Platform.Api.RateLimiting;

public sealed class TenantRateLimiterTests
{
    [Fact]
    public async Task TryAcquireAsync_WithinBudget_Allows()
    {
        using var limiter = new TenantRateLimiter(tokensPerWindow: 2, windowSeconds: 60);

        Assert.True(await limiter.TryAcquireAsync("tenant-a", CancellationToken.None));
        Assert.True(await limiter.TryAcquireAsync("tenant-a", CancellationToken.None));
    }

    [Fact]
    public async Task TryAcquireAsync_WhenBucketExhausted_Rejects()
    {
        using var limiter = new TenantRateLimiter(tokensPerWindow: 2, windowSeconds: 60);

        await limiter.TryAcquireAsync("tenant-a", CancellationToken.None);
        await limiter.TryAcquireAsync("tenant-a", CancellationToken.None);

        Assert.False(await limiter.TryAcquireAsync("tenant-a", CancellationToken.None));
    }

    [Fact]
    public async Task TryAcquireAsync_TenantsAreIsolated()
    {
        using var limiter = new TenantRateLimiter(tokensPerWindow: 1, windowSeconds: 60);

        Assert.True(await limiter.TryAcquireAsync("tenant-a", CancellationToken.None));
        Assert.False(await limiter.TryAcquireAsync("tenant-a", CancellationToken.None));

        Assert.True(await limiter.TryAcquireAsync("tenant-b", CancellationToken.None));
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(10, 0)]
    public void Constructor_WithInvalidConfig_Throws(int tokens, int windowSeconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TenantRateLimiter(tokens, windowSeconds));
    }
}
