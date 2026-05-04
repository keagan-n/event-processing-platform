# Persistence

## Purpose

The MVP uses PostgreSQL to persist jobs.

Persistence makes submitted jobs durable so they survive API restarts and can be processed by the worker.

## Database

Database:

```text
PostgreSQL
```

ORM:

```text
EF Core
```

## Infrastructure Location

Persistence code should live in:

```text
src/Modules/Jobs/Jobs.Infrastructure/
```

Expected infrastructure files:

```text
JobsDbContext.cs
JobRepository.cs
```

Optional later:

```text
Configurations/JobConfiguration.cs
```

## Repository Abstraction

The repository interface should live in:

```text
src/Modules/Jobs/Jobs.Application/IJobRepository.cs
```

Expected methods:

```csharp
Task AddAsync(Job job, CancellationToken cancellationToken);

Task<Job?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken);

Task<Job?> GetNextPendingAsync(CancellationToken cancellationToken);

Task SaveChangesAsync(CancellationToken cancellationToken);
```

## Jobs Table

Table name:

```text
jobs
```

Fields:

| Column | Type | Required | Notes |
|---|---|---:|---|
| id | uuid | yes | Primary key |
| tenant_id | uuid | yes | Tenant ID |
| job_type | text | yes | Job type |
| payload | text | yes | Payload string |
| status | text | yes | Current job status |
| created_utc | timestamp with time zone | yes | Creation time |
| started_utc | timestamp with time zone | no | Processing start time |
| completed_utc | timestamp with time zone | no | Completion time |
| failed_utc | timestamp with time zone | no | Failure time |
| failure_reason | text | no | Failure reason |

## MVP Persistence Behavior

### Submit Job

When `POST /jobs` is called:

1. API maps request to command.
2. Application creates a domain `Job`.
3. Application calls repository `AddAsync`.
4. Application calls repository `SaveChangesAsync`.
5. PostgreSQL stores the job as `Pending`.

### Get Job Status

When `GET /jobs/{jobId}/status` is called:

1. API calls application status service.
2. Application calls repository `GetByIdAsync`.
3. Repository loads job from PostgreSQL.
4. API returns status response.

### Worker Processing

When worker processes jobs:

1. Worker calls application processing service.
2. Application calls repository `GetNextPendingAsync`.
3. Application updates status through domain methods.
4. Repository saves changes to PostgreSQL.

## Concurrency Simplification

For the MVP, concurrency can be simple.

Initial behavior:

- Worker processes one job at a time.
- `GetNextPendingAsync` can return the oldest pending job.
- Advanced row locking is deferred.

Future improvements may include:

- `FOR UPDATE SKIP LOCKED`
- Multiple workers
- Optimistic concurrency
- Retry counters
- Outbox events
- Queue-based processing

## Acceptance Criteria

Persistence is complete when:

- `POST /jobs` inserts a row into PostgreSQL.
- The inserted job has `Pending` status.
- `GET /jobs/{jobId}/status` reads from PostgreSQL.
- Restarting the API does not lose jobs.
- The Worker can update status in PostgreSQL.