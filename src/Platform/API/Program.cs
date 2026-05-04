using DotNetEnv;
using Jobs.Application;
using Jobs.Infrastructure.Persistence;
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

// Register infrastructure services
builder.Services.AddScoped<IJobRepository, JobRepository>();

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

    var response = new SubmitJobResponse(
        JobId: result.JobId,
        TenantId: result.TenantId,
        JobType: result.JobType,
        Status: result.Status,
        CreatedUtc: result.CreatedUtc);

    return Results.Created($"/jobs/{response.JobId}", response);
})
.WithName("SubmitJob");

app.Run();