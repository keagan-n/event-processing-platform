# Current State

## Summary

The project is a simplified job-processing platform.

The current working flow is:

```text
Client -> Platform.API -> Jobs.Application -> Jobs.Domain -> Jobs.Infrastructure -> PostgreSQL
```

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
Redis
rate limiting
AI calls
queue
outbox
retries
DLQ
auth
automated tests
```

## Next Milestone

Worker processing is complete and verified.

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
