using DotNetEnv;
using Jobs.Application;
using Jobs.Infrastructure.Persistence;
using Jobs.Infrastructure.Queue;
using Microsoft.EntityFrameworkCore;
using Platform.Api.Contracts.Jobs;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Register framework services
builder.Services.AddOpenApi();

// Register database
builder.Services.AddDbContext<JobsDbContext>(options =>
{
    var connectionString = Environment.GetEnvironmentVariable("JOBS_DATABASE_CONNECTION_STRING");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "JOBS_DATABASE_CONNECTION_STRING environment variable is not set.");
    }

    options.UseNpgsql(connectionString);
});
// Register application services
builder.Services.AddScoped<IJobSubmissionService, JobSubmissionService>();
builder.Services.AddScoped<IJobStatusService, JobStatusService>();

// Register infrastructure services
builder.Services.AddScoped<IJobRepository, JobRepository>();

var rabbitMqPort = 5672;
var rabbitMqPortRaw = Environment.GetEnvironmentVariable("RABBITMQ_PORT");
if (!string.IsNullOrWhiteSpace(rabbitMqPortRaw) &&
    int.TryParse(rabbitMqPortRaw, out var parsedPort))
{
    rabbitMqPort = parsedPort;
}

var rabbitMqOptions = new RabbitMqOptions
{
    Host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost",
    Port = rabbitMqPort,
    Username = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? "guest",
    Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "guest",
    QueueName = Environment.GetEnvironmentVariable("RABBITMQ_QUEUE_NAME") ?? "jobs.submitted"
};

builder.Services.AddSingleton(rabbitMqOptions);
builder.Services.AddScoped<IJobQueuePublisher, RabbitMqJobQueuePublisher>();

var app = builder.Build();

// Configure development-only tooling
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Simple health endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("Health");

// Submit a new job
app.MapPost("/jobs", async (
    SubmitJobRequest request,
    IJobSubmissionService jobSubmissionService,
    CancellationToken cancellationToken) =>
{
    var command = new SubmitJobCommand(
        TenantId: request.TenantId,
        JobType: request.JobType,
        Payload: request.Payload);

    var result = await jobSubmissionService.SubmitAsync(command, cancellationToken);

    if (!result.Succeeded)
    {
        return Results.BadRequest(new
        {
            message = "Validation failed.",
            errors = result.Errors
        });
    }

    var response = new SubmitJobResponse(
        JobId: result.JobId,
        TenantId: result.TenantId,
        JobType: result.JobType,
        Status: result.Status,
        CreatedUtc: result.CreatedUtc);

    return Results.Created($"/jobs/{response.JobId}", response);
})
.WithName("SubmitJob");

// Get job status
app.MapGet("/jobs/{jobId:guid}/status", async (
    Guid jobId,
    IJobStatusService jobStatusService,
    CancellationToken cancellationToken) =>
{
    var result = await jobStatusService.GetStatusAsync(jobId, cancellationToken);

    if (result is null)
    {
        return Results.NotFound(new
        {
            message = "Job not found.",
            jobId
        });
    }

    var response = new GetJobStatusResponse(
        JobId: result.JobId,
        TenantId: result.TenantId,
        JobType: result.JobType,
        Status: result.Status,
        CreatedUtc: result.CreatedUtc);

    return Results.Ok(response);
})
.WithName("GetJobStatus");

app.Run();
