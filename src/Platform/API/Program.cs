using Jobs.Application;
using Platform.Api.Contracts.Jobs;

var builder = WebApplication.CreateBuilder(args);

// Register framework services
builder.Services.AddOpenApi();

// Register application services
builder.Services.AddScoped<IJobSubmissionService, JobSubmissionService>();

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