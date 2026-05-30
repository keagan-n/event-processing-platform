# Worker Processing Milestone

## Goal

Implement the first working `Platform.Worker` job processor.

The Worker should process jobs from PostgreSQL using the application layer.

## Current Starting Point

Already implemented:

```text
POST /jobs persists a Pending job.
GET /jobs/{jobId}/status returns persisted status.
Job domain supports Processing, Completed, and Failed.
```

## Desired Flow

```text
Platform.Worker
  -> IJobProcessingService
    -> IJobRepository.GetNextPendingAsync
      -> PostgreSQL
    -> Job.MarkProcessing()
    -> SaveChangesAsync()
    -> simulate work
    -> Job.MarkCompleted()
    -> SaveChangesAsync()
```

Failure flow:

```text
Exception during processing
  -> Job.MarkFailed(reason)
  -> SaveChangesAsync()
```

## Files Expected

Likely files to create or update:

```text
src/Modules/Jobs/Jobs.Application/IJobProcessingService.cs
src/Modules/Jobs/Jobs.Application/JobProcessingService.cs
src/Platform/Worker/Program.cs
src/Platform/Worker/JobProcessingBackgroundService.cs
```

Possibly update:

```text
src/Platform/Worker/Platform.Worker.csproj
```

if it needs project references or packages.

## Rules

- Worker must call `IJobProcessingService`.
- Worker must not use `JobsDbContext` directly.
- Worker must not set `job.Status` directly.
- Domain methods enforce status transitions.
- Keep polling simple.
- Process one job at a time for MVP.
- Do not add queues, outbox, retries, DLQ, AI calls, or Redis.

## Polling Behavior

Recommended simple behavior:

```text
Every 2 seconds:
  call ProcessNextPendingJobAsync
  if no job found, wait
  if job processed, continue loop
```

## Acceptance Criteria

This milestone is done when:

1. API submits job with `Pending` status.
2. Worker picks up pending job.
3. Status becomes `Processing`.
4. After simulated work, status becomes `Completed`.
5. `GET /jobs/{jobId}/status` reflects the status changes.
6. DBeaver shows updated status columns.
7. Build succeeds.
