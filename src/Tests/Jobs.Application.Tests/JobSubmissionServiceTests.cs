namespace Jobs.Application.Tests;

using Jobs.Application;
using Jobs.Contracts;
using Jobs.Domain;
using NSubstitute;

public sealed class JobSubmissionServiceTests
{
    private readonly IJobRepository _jobRepository = Substitute.For<IJobRepository>();
    private readonly IOutboxWriter _outboxWriter = Substitute.For<IOutboxWriter>();
    private readonly JobSubmissionService _service;

    public JobSubmissionServiceTests()
    {
        _service = new JobSubmissionService(_jobRepository, _outboxWriter);
    }

    [Fact]
    public async Task SubmitAsync_WithInvalidCommand_ReturnsErrorsAndCallsNoDependencies()
    {
        var command = new SubmitJobCommand(TenantId: " ", JobType: "", Payload: "   ");

        var result = await _service.SubmitAsync(command, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("TenantId is required.", result.Errors);
        Assert.Contains("JobType is required.", result.Errors);
        Assert.Contains("Payload is required.", result.Errors);

        await _jobRepository.DidNotReceiveWithAnyArgs()
            .AddAsync(default!, default);
        await _jobRepository.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(default);
        await _outboxWriter.DidNotReceiveWithAnyArgs()
            .EnqueueAsync(default!, default);
    }

    [Fact]
    public async Task SubmitAsync_WithValidCommand_StagesOutboxMessageBeforeSaving()
    {
        var command = new SubmitJobCommand(
            TenantId: "tenant-1",
            JobType: "report",
            Payload: "{\"data\":\"hello\"}");

        var result = await _service.SubmitAsync(command, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotEqual(Guid.Empty, result.JobId);

        // Transactional outbox: the message is staged in the same unit of work
        // and committed by the single SaveChangesAsync.
        Received.InOrder(() =>
        {
            _jobRepository.AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
            _outboxWriter.EnqueueAsync(
                Arg.Any<JobSubmittedMessage>(),
                Arg.Any<CancellationToken>());
            _jobRepository.SaveChangesAsync(Arg.Any<CancellationToken>());
        });

        await _outboxWriter.Received(1).EnqueueAsync(
            Arg.Is<JobSubmittedMessage>(message => message.JobId == result.JobId),
            Arg.Any<CancellationToken>());
        await _jobRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitAsync_WithPaddedInputs_TrimsBeforePersisting()
    {
        Job? persistedJob = null;

        await _jobRepository.AddAsync(
            Arg.Do<Job>(job => persistedJob = job),
            Arg.Any<CancellationToken>());

        var command = new SubmitJobCommand(
            TenantId: "  tenant-1  ",
            JobType: "  report  ",
            Payload: "  {\"data\":\"hello\"}  ");

        var result = await _service.SubmitAsync(command, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("tenant-1", result.TenantId);
        Assert.Equal("report", result.JobType);

        Assert.NotNull(persistedJob);
        Assert.Equal("tenant-1", persistedJob.TenantId);
        Assert.Equal("report", persistedJob.JobType);
        Assert.Equal("{\"data\":\"hello\"}", persistedJob.Payload);
    }
}
