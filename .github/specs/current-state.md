# Current State

## Summary

The project is a simplified job-processing platform.

The current working flow is:

```text
Client -> Platform.API -> Jobs.Application -> Jobs.Domain -> Jobs.Infrastructure -> PostgreSQL
API publishes JobSubmittedMessage to RabbitMQ after persistence succeeds.
Platform.Worker consumes JobSubmittedMessage from RabbitMQ and processes jobs through Jobs.Application.
```

PostgreSQL is the system of record for all job state.

## Implemented

### API

Implemented endpoints:

```text
GET /health
POST /jobs
GET /jobs/{jobId}/status
```

`POST /jobs` persists jobs to PostgreSQL and publishes `JobSubmittedMessage` to RabbitMQ after persistence succeeds.
`POST /jobs` validates required fields (`TenantId`, `JobType`, `Payload`).
Invalid submit requests return `400 Bad Request`.
Valid submit requests return `201 Created`.

`GET /jobs/{jobId}/status` reads status from PostgreSQL.

### Domain

`Job` includes:

```text
Id
TenantId
JobType
Payload
Status
CreatedUtc
StartedUtc
CompletedUtc
FailedUtc
FailureReason
```

`TenantId` is a string.

`JobId` is a Guid.

`JobStatus` includes:

```text
Pending
Processing
Completed
Failed
```

Domain methods:

```text
Create(...)
MarkProcessing()
MarkCompleted()
MarkFailed(reason)
```

Allowed transitions:

```text
Pending -> Processing
Processing -> Completed
Processing -> Failed
```

### Application

Implemented application services:

```text
IJobSubmissionService
JobSubmissionService
IJobStatusService
JobStatusService
IJobRepository
IJobProcessingService
JobProcessingService
IJobQueuePublisher
```

`IJobProcessingService.ProcessJobAsync(Guid jobId, CancellationToken)` loads a job by ID, skips non-Pending jobs, and processes Pending jobs through the domain lifecycle.

### Infrastructure

Implemented:

```text
JobsDbContext
JobRepository
EF Core migrations
PostgreSQL persistence
RabbitMqOptions
RabbitMqJobQueuePublisher
```

### Worker

`Platform.Worker` consumes `JobSubmittedMessage` from RabbitMQ using `JobSubmittedMessageConsumerService`.
For each message, the worker resolves `IJobProcessingService` in a DI scope and calls `ProcessJobAsync`.
Messages are acked after successful processing and nacked without requeue on failure.
PostgreSQL polling worker has been removed.

### Local Development

PostgreSQL runs through Docker Compose.
RabbitMQ local infrastructure runs through Docker Compose.
`JobSubmittedMessage` contract exists.
`IJobQueuePublisher` abstraction exists.
RabbitMQ publisher implementation exists.
`POST /jobs` publishes `JobSubmittedMessage` to RabbitMQ after PostgreSQL persistence succeeds.
Worker consumes `JobSubmittedMessage` from RabbitMQ and processes jobs.

Database can be inspected with DBeaver.

Secrets are loaded from `.env` using DotNetEnv.

## Not Implemented

```text
AI calls
retries
rate limiting
DLQ
auth
automated tests
```

Removed from active scope:

```text
Redis
OutboxRelay
outbox pattern
```

## Next Milestone

Queue-based dispatch is fully implemented (Milestones 1-4 complete).

Next active implementation order:

```text
1) Python AI service integration
2) Retries after AI integration
3) Rate limiting after queue + AI + retries
```

Rate limiting constraint:

```text
Do not use Redis-backed rate limiting by default.
Prefer PostgreSQL-backed or simple in-memory MVP rate limiting unless explicitly redesigned.
```

Verified lifecycle transitions:

```text
Pending -> Processing -> Completed
Pending -> Processing -> Failed
```

Forced failure verification exists via payload marker:

```json
{ "forceFailure": true }
```

`Platform.Worker` delegates processing to `Jobs.Application` through `IJobProcessingService`.

Outbox tradeoff note:

```text
For this portfolio MVP, DB save + queue publish is intentionally non-atomic (simpler path).
If publish fails after DB save, a queue message can be lost.
Production systems use outbox/transactional messaging to solve this, but outbox is intentionally out of active scope.
```
