# Worker Processing Milestone

Status: Complete

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

## Acceptance Checklist

- [x] Docker PostgreSQL running
- [x] API running
- [x] Worker running
- [x] Normal job transitions `Pending -> Processing -> Completed`
- [x] Forced-failure job transitions `Pending -> Processing -> Failed`
- [x] DBeaver shows lifecycle timestamps and failure reason where applicable
