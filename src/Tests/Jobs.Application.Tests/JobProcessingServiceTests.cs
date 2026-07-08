namespace Jobs.Application.Tests;

using Jobs.Application;
using Jobs.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

public sealed class JobProcessingServiceTests
{
    private readonly IJobRepository _jobRepository = Substitute.For<IJobRepository>();
    private readonly IAiAnalysisClient _aiAnalysisClient = Substitute.For<IAiAnalysisClient>();
    private readonly JobProcessingService _service;

    public JobProcessingServiceTests()
    {
        _service = new JobProcessingService(
            _jobRepository,
            _aiAnalysisClient,
            new JobProcessingOptions { MaxAttempts = 3 },
            NullLogger<JobProcessingService>.Instance);
    }

    [Fact]
    public async Task ProcessJobAsync_WhenJobIsMissing_Throws()
    {
        var jobId = Guid.NewGuid();

        _jobRepository.GetByIdAsync(jobId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Job?>(null));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ProcessJobAsync(jobId, CancellationToken.None));
    }

    [Fact]
    public async Task ProcessJobAsync_WhenJobIsNotPending_SkipsWithoutSaving()
    {
        var job = Job.Create("tenant-1", "report", "{\"data\":\"hello\"}");
        job.MarkProcessing();

        _jobRepository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Job?>(job));

        var result = await _service.ProcessJobAsync(job.Id, CancellationToken.None);

        Assert.Equal(JobProcessingOutcome.Skipped, result.Outcome);
        Assert.Equal(JobStatus.Processing, job.Status);
        await _jobRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _aiAnalysisClient.DidNotReceive().AnalyzeAsync(
            Arg.Any<AiAnalysisRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessJobAsync_WhenJobAlreadyCompleted_IsIdempotentNoOp()
    {
        var job = Job.Create("tenant-1", "report", "{\"data\":\"hello\"}");
        job.MarkProcessing();
        job.MarkCompleted("Done.");

        _jobRepository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Job?>(job));

        var result = await _service.ProcessJobAsync(job.Id, CancellationToken.None);

        Assert.Equal(JobProcessingOutcome.Skipped, result.Outcome);
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal("Done.", job.Result);
        await _jobRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessJobAsync_WithPendingJob_CallsAiClientAndCompletes()
    {
        var job = Job.Create("tenant-1", "report", "{\"data\":\"hello\"}");

        _jobRepository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Job?>(job));

        _aiAnalysisClient.AnalyzeAsync(Arg.Any<AiAnalysisRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AiAnalysisResult("Payload summary.", "stub-analyzer-v1")));

        var result = await _service.ProcessJobAsync(job.Id, CancellationToken.None);

        Assert.Equal(JobProcessingOutcome.Completed, result.Outcome);
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal("Payload summary.", job.Result);
        Assert.NotNull(job.StartedUtc);
        Assert.NotNull(job.CompletedUtc);
        Assert.Null(job.FailureReason);
        await _jobRepository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _aiAnalysisClient.Received(1).AnalyzeAsync(
            Arg.Is<AiAnalysisRequest>(request =>
                request.JobId == job.Id &&
                request.JobType == job.JobType &&
                request.Payload == job.Payload),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessJobAsync_WhenFirstAttemptFails_SchedulesRetry()
    {
        var job = Job.Create("tenant-1", "report", "{\"data\":\"hello\"}");

        _jobRepository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Job?>(job));

        _aiAnalysisClient.AnalyzeAsync(Arg.Any<AiAnalysisRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AiAnalysisResult>(
                new HttpRequestException("AI service is unavailable.")));

        var result = await _service.ProcessJobAsync(job.Id, CancellationToken.None);

        Assert.Equal(JobProcessingOutcome.RetryScheduled, result.Outcome);
        Assert.Equal("AI service is unavailable.", result.FailureReason);
        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Equal(1, job.AttemptCount);
        Assert.Null(job.FailureReason);
        await _jobRepository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessJobAsync_WithForceFailurePayload_SchedulesRetryThenFollowsRetryPath()
    {
        var job = Job.Create("tenant-1", "report", "{\"forceFailure\":true}");

        _jobRepository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Job?>(job));

        var result = await _service.ProcessJobAsync(job.Id, CancellationToken.None);

        Assert.Equal(JobProcessingOutcome.RetryScheduled, result.Outcome);
        Assert.Equal("Forced failure requested via payload.", result.FailureReason);
        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Equal(1, job.AttemptCount);
        await _aiAnalysisClient.DidNotReceive().AnalyzeAsync(
            Arg.Any<AiAnalysisRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessJobAsync_WhenFinalAttemptFails_MarksFailedPermanently()
    {
        var job = Job.Create("tenant-1", "report", "{\"data\":\"hello\"}");
        job.MarkProcessing();
        job.MarkPendingForRetry();
        job.MarkProcessing();
        job.MarkPendingForRetry();
        Assert.Equal(2, job.AttemptCount);

        _jobRepository.GetByIdAsync(job.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Job?>(job));

        _aiAnalysisClient.AnalyzeAsync(Arg.Any<AiAnalysisRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AiAnalysisResult>(
                new HttpRequestException("AI service is unavailable.")));

        var result = await _service.ProcessJobAsync(job.Id, CancellationToken.None);

        Assert.Equal(JobProcessingOutcome.FailedPermanently, result.Outcome);
        Assert.Equal("AI service is unavailable.", result.FailureReason);
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal("AI service is unavailable.", job.FailureReason);
        Assert.NotNull(job.FailedUtc);
        Assert.Null(job.Result);
        await _jobRepository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
