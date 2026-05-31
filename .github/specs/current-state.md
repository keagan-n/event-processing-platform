# Current State

## Summary

The project is a simplified job-processing platform.

The current working flow is:

```text
Client -> Platform.API -> Jobs.Application -> Jobs.Domain -> Jobs.Infrastructure -> PostgreSQL
```

Current worker behavior is PostgreSQL polling. This is the working MVP behavior, not the final queue-based direction.

## Implemented

### API

Implemented endpoints:

```text
GET /health
POST /jobs
GET /jobs/{jobId}/status
```

`POST /jobs` persists jobs to PostgreSQL.
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
```

### Infrastructure

Implemented:

```text
JobsDbContext
JobRepository
EF Core migrations
PostgreSQL persistence
```

### Local Development

PostgreSQL runs through Docker Compose.

Database can be inspected with DBeaver.

Secrets are loaded from `.env` using DotNetEnv.

## Not Implemented

```text
Queue-based dispatch
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

Worker processing is complete and verified (polling MVP).

Next active implementation order:

```text
1) Queue-based job dispatch
2) Python AI service integration
3) Retries after AI integration
4) Rate limiting after queue + AI + retries
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
Worker lifecycle behavior is unchanged by submit validation hardening.

Outbox tradeoff note:

```text
For this portfolio MVP, DB save + queue publish is intentionally non-atomic (simpler path).
If publish fails after DB save, a queue message can be lost.
Production systems use outbox/transactional messaging to solve this, but outbox is intentionally out of active scope.
```
