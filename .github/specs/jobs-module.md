# Jobs Module

## Purpose

The Jobs module owns the lifecycle of submitted jobs.

It is responsible for:

- Creating jobs
- Persisting jobs
- Tracking job status
- Processing pending jobs
- Enforcing valid status transitions

## Domain Concepts

### Job

A `Job` represents a unit of work submitted by a client.

Current MVP fields:

- `Id`
- `TenantId`
- `JobType`
- `Payload`
- `Status`
- `CreatedUtc`
- `StartedUtc`
- `CompletedUtc`
- `FailedUtc`
- `FailureReason`

### JobStatus

The MVP status values are:

- `Pending`
- `Processing`
- `Completed`
- `Failed`

## Status Lifecycle

Allowed transitions:

Pending -> Processing
Processing -> Completed
Processing -> Failed

Invalid transitions should be rejected by domain methods.

Examples of invalid transitions:

Pending -> Completed
Completed -> Processing
Failed -> Processing
Completed -> Failed
Domain Rules

The domain should enforce:

TenantId is required.
JobType is required.
Payload is required.
A new job starts as Pending.
A job can only be marked Processing from Pending.
A job can only be marked Completed from Processing.
A job can only be marked Failed from Processing.
Domain Methods

The Job entity should expose methods similar to:

Create(...)
MarkProcessing(...)
MarkCompleted(...)
MarkFailed(...)

Status should not be randomly mutated by API, Worker, or Infrastructure code.

Application Services

The Jobs application layer should include:

IJobSubmissionService

Used by the API to submit a new job.

Responsibilities:

Accept a submit command
Create a domain job
Persist the job
Return a submit result
IJobStatusService

Used by the API to query job status.

Responsibilities:

Load a job by ID
Return status information
Return null or not-found behavior when missing
IJobProcessingService

Used by the Worker to process jobs.

Responsibilities:

Find the next pending job
Mark it Processing
Simulate processing
Mark it Completed
Mark it Failed if processing throws
Repository Abstraction

The repository abstraction should live in Jobs.Application.

Expected methods:

AddAsync
GetByIdAsync
GetNextPendingAsync
SaveChangesAsync

The application layer depends on this abstraction.

The infrastructure layer implements it using EF Core and PostgreSQL.

Deferred Behavior

The Jobs module should not implement these yet:

Queue publishing
Outbox events
Redis rate limiting
AI service execution
Retries
DLQ
Parallel job processing
Tenant quotas