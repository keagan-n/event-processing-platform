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
- This milestone intentionally used PostgreSQL polling and is complete.

## Polling Behavior

Recommended simple behavior:

```text
Every 2 seconds:
  call ProcessNextPendingJobAsync
  if no job found, wait
  if job processed, continue loop
```

## Next Direction (Not Yet Implemented)

Queue-based dispatch is the next active implementation direction:

```text
API persists job
API publishes queue message with jobId
Worker consumes queue message
Worker loads job by jobId
Worker processes through Jobs.Application
```

After queue dispatch:

```text
Python AI integration
then retries
then rate limiting
```

Rate limiting note:

```text
Future rate limiting should not be Redis-backed unless explicitly redesigned.
Prefer PostgreSQL-backed or in-memory MVP options.
```

Outbox tradeoff:

```text
Without outbox, DB save + queue publish is not atomic.
A queue publish failure after DB save can lose dispatch.
Accepted for this portfolio MVP; production systems use outbox/transactional messaging.
```

## Acceptance Checklist

- [x] Docker PostgreSQL running
- [x] API running
- [x] Worker running
- [x] Normal job transitions `Pending -> Processing -> Completed`
- [x] Forced-failure job transitions `Pending -> Processing -> Failed`
- [x] DBeaver shows lifecycle timestamps and failure reason where applicable
