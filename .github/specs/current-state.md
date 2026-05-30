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
Platform.Worker processing
IJobProcessingService fully wired into Worker
background polling loop
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

Implement Worker processing:

```text
Pending -> Processing -> Completed
```

with failure support:

```text
Processing -> Failed
```
