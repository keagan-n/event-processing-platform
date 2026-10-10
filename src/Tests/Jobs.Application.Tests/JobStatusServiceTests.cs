namespace Jobs.Application.Tests;

using Jobs.Domain;
using NSubstitute;

public sealed class JobStatusServiceTests
{
    private readonly IJobRepository _repository = Substitute.For<IJobRepository>();

    [Fact]
    public async Task GetStatusAsync_WhenMissing_ReturnsNull()
    {
        var service = new JobStatusService(_repository);
        Assert.Null(await service.GetStatusAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetStatusAsync_WhenFailed_ExposesReasonAndRetryCount()
    {
        var job = Job.Create("tenant-demo", "analysis", "text");
        job.MarkProcessing();
        job.MarkPendingForRetry();
        job.MarkProcessing();
        job.MarkFailed("AI service unavailable.");
        _repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var result = await new JobStatusService(_repository).GetStatusAsync(job.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Failed", result.Status);
        Assert.Equal("AI service unavailable.", result.FailureReason);
        Assert.Equal(1, result.AttemptCount);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task GetStatusAsync_WhenCompleted_ExposesResultAndIdentity()
    {
        var job = Job.Create("tenant-demo", "analysis", "text");
        job.MarkProcessing();
        job.MarkCompleted("Summary.");
        _repository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var result = await new JobStatusService(_repository).GetStatusAsync(job.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(job.Id, result.JobId);
        Assert.Equal(job.TenantId, result.TenantId);
        Assert.Equal(job.JobType, result.JobType);
        Assert.Equal("Completed", result.Status);
        Assert.Equal("Summary.", result.Result);
        Assert.Null(result.FailureReason);
    }
}
