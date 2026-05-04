# Development Plan

## MVP Goal

Build the simplest possible job-processing platform:

```text
POST job -> persist in PostgreSQL -> worker processes job -> status updates -> GET status
```

## Milestone 1: Current Vertical Slice

Status: mostly complete.

### Already Done

- Repo scaffolded
- Solution builds
- Minimal API runs locally
- `GET /health` works
- `POST /jobs` works
- API calls Jobs.Application
- Jobs.Application calls Jobs.Domain
- Domain creates a valid Job
- API returns `201 Created`

### Current Limitation

Jobs are not persisted yet.

## Milestone 2: Add PostgreSQL Persistence

### Goal

Persist submitted jobs to PostgreSQL.

### Tasks

- Add EF Core PostgreSQL package references.
- Create `JobsDbContext`.
- Configure Job persistence.
- Create `IJobRepository` if missing.
- Implement `JobRepository`.
- Register DbContext and repository in API.
- Update job submission flow to save jobs.
- Create and apply database migration.

### Acceptance Criteria

- `POST /jobs` stores a job in PostgreSQL.
- Stored job has `Pending` status.
- Job remains after API restart.
- Solution builds.
- API runs locally.

## Milestone 3: Add Job Status Endpoint

### Goal

Allow clients to query current job status.

### Tasks

- Add status query application result.
- Add status query application service.
- Add API response contract.
- Add `GET /jobs/{jobId}/status`.
- Return `404` when job does not exist.

### Acceptance Criteria

- Existing job returns status.
- Missing job returns `404`.
- API does not use EF Core directly.
- Application uses repository abstraction.

## Milestone 4: Add Worker Processing

### Goal

Process pending jobs in the background.

### Tasks

- Register Jobs infrastructure dependencies in Worker.
- Add background service.
- Poll for pending jobs.
- Mark job `Processing`.
- Simulate work.
- Mark job `Completed`.
- Mark job `Failed` on exception.

### Acceptance Criteria

- Submitted job starts as `Pending`.
- Worker changes job to `Processing`.
- Worker changes job to `Completed`.
- `GET /jobs/{jobId}/status` reflects updates.

## Milestone 5: Improve Domain Lifecycle

### Goal

Make status transitions explicit and safe.

### Tasks

- Add domain methods:
  - `MarkProcessing`
  - `MarkCompleted`
  - `MarkFailed`
- Add timestamp fields:
  - `StartedUtc`
  - `CompletedUtc`
  - `FailedUtc`
- Add `FailureReason`.
- Block invalid transitions.

### Acceptance Criteria

- Status cannot be changed arbitrarily.
- Invalid transitions throw domain exceptions.
- Worker uses domain methods.

## Milestone 6: README Cleanup

### Goal

Document the MVP honestly and professionally.

### Tasks

- Update README project description.
- Add architecture diagram.
- Add MVP flow.
- Add repo structure.
- Add run instructions.
- Add Postman test instructions.
- Add deferred features.

### Acceptance Criteria

- README accurately reflects current functionality.
- README does not claim unbuilt features.
- README is useful for recruiters and interviewers.

## Deferred Future Milestones

The following are intentionally deferred:

- Redis rate limiting
- Python AI service execution
- Queue-based processing
- Outbox relay
- Retry policies
- Dead-letter queue
- Authentication
- Authorization
- k6 performance testing
- Advanced observability
- Multi-tenant quotas