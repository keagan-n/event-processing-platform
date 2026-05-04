namespace Jobs.Application;

public interface IJobSubmissionService
{
    Task<SubmitJobResult> SubmitAsync(SubmitJobCommand command, CancellationToken cancellationToken);
}