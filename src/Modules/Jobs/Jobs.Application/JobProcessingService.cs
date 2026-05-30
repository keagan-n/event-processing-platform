namespace Jobs.Application;

public sealed class JobProcessingService : IJobProcessingService
{
    private readonly IJobRepository _jobRepository;

    public JobProcessingService(IJobRepository jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public async Task<bool> ProcessNextPendingJobAsync(CancellationToken cancellationToken)
    {
        var job = await _jobRepository.GetNextPendingAsync(cancellationToken);

        if (job is null)
        {
            return false;
        }

        var markedProcessing = false;

        try
        {
            job.MarkProcessing();
            markedProcessing = true;
            await _jobRepository.SaveChangesAsync(cancellationToken);

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

            job.MarkCompleted();
            await _jobRepository.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (Exception exception)
        {
            if (markedProcessing)
            {
                job.MarkFailed(exception.Message);
                await _jobRepository.SaveChangesAsync(cancellationToken);
            }

            throw;
        }
    }
}
