namespace Jobs.Domain.Tests;

using Jobs.Domain;

public sealed class JobTests
{
    [Fact]
    public void Create_ProducesPendingJobWithCreatedUtcSet()
    {
        var before = DateTime.UtcNow;

        var job = Job.Create("tenant-1", "report", "{\"data\":\"hello\"}");

        var after = DateTime.UtcNow;

        Assert.NotEqual(Guid.Empty, job.Id);
        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.InRange(job.CreatedUtc, before, after);
        Assert.Null(job.StartedUtc);
        Assert.Null(job.CompletedUtc);
        Assert.Null(job.FailedUtc);
        Assert.Null(job.FailureReason);
        Assert.Null(job.Result);
    }

    [Fact]
    public void MarkProcessing_FromPending_SetsProcessingAndStartedUtc()
    {
        var job = CreateJobInStatus(JobStatus.Pending);

        job.MarkProcessing();

        Assert.Equal(JobStatus.Processing, job.Status);
        Assert.NotNull(job.StartedUtc);
    }

    [Fact]
    public void MarkCompleted_FromProcessing_SetsCompletedAndCompletedUtc()
    {
        var job = CreateJobInStatus(JobStatus.Processing);

        job.MarkCompleted("Analysis summary.");

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.NotNull(job.CompletedUtc);
    }

    [Fact]
    public void MarkCompleted_FromProcessing_SetsResult()
    {
        var job = CreateJobInStatus(JobStatus.Processing);

        job.MarkCompleted("Analysis summary.");

        Assert.Equal("Analysis summary.", job.Result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MarkCompleted_WithMissingResult_Throws(string result)
    {
        var job = CreateJobInStatus(JobStatus.Processing);

        Assert.Throws<ArgumentException>(() => job.MarkCompleted(result));
        Assert.Equal(JobStatus.Processing, job.Status);
        Assert.Null(job.Result);
    }

    [Fact]
    public void MarkFailed_FromProcessing_SetsFailedUtcAndFailureReason()
    {
        var job = CreateJobInStatus(JobStatus.Processing);

        job.MarkFailed("Something went wrong.");

        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.NotNull(job.FailedUtc);
        Assert.Equal("Something went wrong.", job.FailureReason);
    }

    [Theory]
    [InlineData(JobStatus.Processing)]
    [InlineData(JobStatus.Completed)]
    [InlineData(JobStatus.Failed)]
    public void MarkProcessing_FromInvalidStatus_Throws(JobStatus fromStatus)
    {
        var job = CreateJobInStatus(fromStatus);

        Assert.Throws<InvalidOperationException>(() => job.MarkProcessing());
        Assert.Equal(fromStatus, job.Status);
    }

    [Theory]
    [InlineData(JobStatus.Pending)]
    [InlineData(JobStatus.Completed)]
    [InlineData(JobStatus.Failed)]
    public void MarkCompleted_FromInvalidStatus_Throws(JobStatus fromStatus)
    {
        var job = CreateJobInStatus(fromStatus);

        Assert.Throws<InvalidOperationException>(() => job.MarkCompleted("Analysis summary."));
        Assert.Equal(fromStatus, job.Status);
    }

    [Theory]
    [InlineData(JobStatus.Pending)]
    [InlineData(JobStatus.Completed)]
    [InlineData(JobStatus.Failed)]
    public void MarkFailed_FromInvalidStatus_Throws(JobStatus fromStatus)
    {
        var job = CreateJobInStatus(fromStatus);

        Assert.Throws<InvalidOperationException>(() => job.MarkFailed("reason"));
        Assert.Equal(fromStatus, job.Status);
    }

    [Fact]
    public void MarkPendingForRetry_FromProcessing_ReturnsToPendingAndIncrementsAttemptCount()
    {
        var job = CreateJobInStatus(JobStatus.Processing);
        Assert.Equal(0, job.AttemptCount);

        job.MarkPendingForRetry();

        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Equal(1, job.AttemptCount);
    }

    [Fact]
    public void MarkPendingForRetry_RepeatedCycles_AccumulateAttemptCount()
    {
        var job = CreateJobInStatus(JobStatus.Processing);

        job.MarkPendingForRetry();
        job.MarkProcessing();
        job.MarkPendingForRetry();

        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Equal(2, job.AttemptCount);
    }

    [Theory]
    [InlineData(JobStatus.Pending)]
    [InlineData(JobStatus.Completed)]
    [InlineData(JobStatus.Failed)]
    public void MarkPendingForRetry_FromInvalidStatus_Throws(JobStatus fromStatus)
    {
        var job = CreateJobInStatus(fromStatus);

        Assert.Throws<InvalidOperationException>(() => job.MarkPendingForRetry());
        Assert.Equal(fromStatus, job.Status);
    }

    private static Job CreateJobInStatus(JobStatus status)
    {
        var job = Job.Create("tenant-1", "report", "{\"data\":\"hello\"}");

        switch (status)
        {
            case JobStatus.Pending:
                return job;
            case JobStatus.Processing:
                job.MarkProcessing();
                return job;
            case JobStatus.Completed:
                job.MarkProcessing();
                job.MarkCompleted("Completed for test setup.");
                return job;
            case JobStatus.Failed:
                job.MarkProcessing();
                job.MarkFailed("Failure for test setup.");
                return job;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported test status.");
        }
    }
}
