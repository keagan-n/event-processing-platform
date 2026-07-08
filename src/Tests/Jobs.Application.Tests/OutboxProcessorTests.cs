namespace Jobs.Application.Tests;

using System.Text.Json;
using Jobs.Application;
using Jobs.Contracts;
using NSubstitute;

public sealed class OutboxProcessorTests
{
    private readonly IOutboxStore _outboxStore = Substitute.For<IOutboxStore>();
    private readonly IJobQueuePublisher _jobQueuePublisher = Substitute.For<IJobQueuePublisher>();
    private readonly OutboxProcessor _processor;

    public OutboxProcessorTests()
    {
        _processor = new OutboxProcessor(
            _outboxStore,
            _jobQueuePublisher,
            new OutboxOptions { BatchSize = 20 });
    }

    private static OutboxItem ItemFor(Guid jobId) =>
        new(
            Id: Guid.NewGuid(),
            Type: nameof(JobSubmittedMessage),
            Payload: JsonSerializer.Serialize(new JobSubmittedMessage(jobId)));

    [Fact]
    public async Task ProcessBatchAsync_WithNoPendingMessages_PublishesNothing()
    {
        _outboxStore.GetUnprocessedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OutboxItem>>(Array.Empty<OutboxItem>()));

        var published = await _processor.ProcessBatchAsync(CancellationToken.None);

        Assert.Equal(0, published);
        await _jobQueuePublisher.DidNotReceiveWithAnyArgs()
            .PublishJobSubmittedAsync(default!, default);
    }

    [Fact]
    public async Task ProcessBatchAsync_PublishesPendingMessagesAndMarksProcessed()
    {
        var jobId1 = Guid.NewGuid();
        var jobId2 = Guid.NewGuid();
        var item1 = ItemFor(jobId1);
        var item2 = ItemFor(jobId2);

        _outboxStore.GetUnprocessedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OutboxItem>>(new[] { item1, item2 }));

        var published = await _processor.ProcessBatchAsync(CancellationToken.None);

        Assert.Equal(2, published);
        await _jobQueuePublisher.Received(1).PublishJobSubmittedAsync(
            Arg.Is<JobSubmittedMessage>(message => message.JobId == jobId1),
            Arg.Any<CancellationToken>());
        await _jobQueuePublisher.Received(1).PublishJobSubmittedAsync(
            Arg.Is<JobSubmittedMessage>(message => message.JobId == jobId2),
            Arg.Any<CancellationToken>());
        await _outboxStore.Received(1).MarkProcessedAsync(item1.Id, Arg.Any<CancellationToken>());
        await _outboxStore.Received(1).MarkProcessedAsync(item2.Id, Arg.Any<CancellationToken>());
        await _outboxStore.DidNotReceiveWithAnyArgs().MarkFailedAsync(default, default!, default);
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenPublishFails_RecordsErrorAndContinues()
    {
        var failingItem = ItemFor(Guid.NewGuid());
        var healthyJobId = Guid.NewGuid();
        var healthyItem = ItemFor(healthyJobId);

        _outboxStore.GetUnprocessedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OutboxItem>>(new[] { failingItem, healthyItem }));

        _jobQueuePublisher.PublishJobSubmittedAsync(
                Arg.Any<JobSubmittedMessage>(),
                Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException(new InvalidOperationException("RabbitMQ is down.")),
                _ => Task.CompletedTask);

        var published = await _processor.ProcessBatchAsync(CancellationToken.None);

        Assert.Equal(1, published);
        await _outboxStore.Received(1).MarkFailedAsync(
            failingItem.Id,
            "RabbitMQ is down.",
            Arg.Any<CancellationToken>());
        await _outboxStore.DidNotReceive().MarkProcessedAsync(
            failingItem.Id,
            Arg.Any<CancellationToken>());
        await _outboxStore.Received(1).MarkProcessedAsync(
            healthyItem.Id,
            Arg.Any<CancellationToken>());
    }
}
