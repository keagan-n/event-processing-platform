# Current State

## Summary

The completion-plan feature set is implemented. This is a single-instance demo with explicit crash-recovery limitations, not a production reliability guarantee (see below).

The working flow is:

```text
Client -> Platform.API -> validate -> per-tenant rate limit check
       -> persist Job + OutboxMessage in ONE PostgreSQL transaction
OutboxDispatcherService (in API host) -> polls outbox -> publishes JobSubmittedMessage to RabbitMQ
Platform.Worker -> consumes jobs.submitted (prefetch-bounded) -> IJobProcessingService
                -> calls Python ai-service POST /v1/analyze (retry w/ exponential backoff + jitter)
                -> success: MarkCompleted(result) | failure: retry up to MaxAttempts then MarkFailed + DLQ
Client -> GET /jobs/{jobId}/status -> status, result, attemptCount
```

PostgreSQL is the system of record for all job and outbox state.

## Implemented

### API (`Platform.Api`)

- `GET /health`
- `POST /jobs` — validates required fields, trimmed identifier lengths (255 maximum), and trimmed payload size (64 KiB UTF-8 maximum, 400). Kestrel caps the whole request body at 256 KiB (413). Rate-limits per tenant (429 + Retry-After), persists job + outbox row atomically (201). No inline RabbitMQ publish. `Location` points to `/jobs/{jobId}/status`.
- `GET /jobs/{jobId}/status` — returns `jobId`, `tenantId`, `jobType`, `status`, `createdUtc`, `result` (nullable), `attemptCount`, `failureReason` (nullable).
- `TenantRateLimiter` — in-memory `PartitionedRateLimiter` token bucket per TenantId. Env: `RATE_LIMIT_TOKENS` (10), `RATE_LIMIT_WINDOW_SECONDS` (60). Applied inside the endpoint because the partition key comes from the request body.
- `OutboxDispatcherService` — BackgroundService polling every `OUTBOX_POLL_SECONDS` (2), publishing batches of 20 via `IOutboxProcessor`; failed rows keep `Error` and stay unprocessed for the next tick.
- `APPLY_MIGRATIONS=true` env applies EF migrations on startup (used by docker compose).
- Serilog structured JSON console logging + request logging.

### Domain (`Jobs.Domain`)

`Job`: `Id`, `TenantId`, `JobType`, `Payload`, `Status`, `CreatedUtc`, `StartedUtc`, `CompletedUtc`, `FailedUtc`, `FailureReason`, `Result`, `AttemptCount`.

Transitions (all others throw `InvalidOperationException`):

```text
Pending -> Processing            MarkProcessing()
Processing -> Completed          MarkCompleted(result)
Processing -> Failed             MarkFailed(reason)
Processing -> Pending (retry)    MarkPendingForRetry()  [increments AttemptCount]
```

### Application (`Jobs.Application`)

- `JobSubmissionService` — validates, trims, stages job + outbox message, single `SaveChangesAsync` (transactional outbox). Depends on `IOutboxWriter`, not the queue publisher.
- `JobProcessingService` — returns `JobProcessingResult` (`Completed` | `Skipped` | `RetryScheduled` | `FailedPermanently`). Non-Pending jobs are skipped (idempotency guard for at-least-once delivery). On failure: `MarkPendingForRetry` while `AttemptCount + 1 < MaxAttempts`, else `MarkFailed`. AI failures return outcomes; cancellation and persistence failures propagate. The completion save is outside the AI-failure handler so a database error is not masked by an illegal status transition. Plain text and non-object JSON payloads are accepted. `forceFailure` payload follows the same retry-then-DLQ path.
- `JobProcessingOptions.MaxAttempts` (env `JOB_MAX_ATTEMPTS`, default 3).
- `OutboxProcessor` / `IOutboxStore` / `IOutboxWriter` / `OutboxOptions` — outbox orchestration lives in Application; EF implementations in Infrastructure.
- `IAiAnalysisClient` + `AiAnalysisRequest`/`AiAnalysisResult` records.
- `IJobQueuePublisher` — `PublishJobSubmittedAsync`, `PublishJobRetryAsync` (republish to main queue), `PublishJobFailedAsync` (DLQ + `x-failure-reason` header).

### Infrastructure (`Jobs.Infrastructure`)

- `JobsDbContext` — `jobs` + `outbox_messages` tables. Migrations: InitialJobsSchema, AddJobLifecycleFields, AddJobResult, AddJobAttemptCount, AddOutboxMessages.
- `JobRepository`, `EfOutboxWriter` (stages in caller's unit of work), `EfOutboxStore`.
- `RabbitMqJobQueuePublisher` — declares durable queues, sends persistent messages with `MessageId`, uses mandatory routing and waits for broker confirms (10-second timeout) before returning; DLQ name from `RabbitMqOptions.DeadLetterQueueName` (env `RABBITMQ_DLQ_NAME`, default `jobs.submitted.dlq`).
- `HttpAiAnalysisClient` — typed HttpClient, snake_case JSON matching the FastAPI contract; validates the response job ID and nonempty summary/model.

### Worker (`Platform.Worker`)

- `JobSubmittedMessageConsumerService` — consumes `jobs.submitted` with `BasicQos` prefetch (env `RABBITMQ_PREFETCH`, default 4; deliberate backpressure protecting the AI service). Maps outcomes: Completed/Skipped -> ack; RetryScheduled -> republish + ack; FailedPermanently -> DLQ publish + ack. Unexpected exceptions -> nack without requeue.
- AI client registered with a resilience handler: 3 retries, exponential backoff + jitter (transient 5xx/timeout/connection failures).
- Serilog structured JSON console logging.

### AI Service (`ai-service/`, Python 3.11 / FastAPI)

- `GET /health` -> `{status, provider}`; `POST /v1/analyze` `{job_id, job_type, payload}` -> `{job_id, summary, model, latency_ms}`.
- Providers: `stub` (default — deterministic, free, no keys) and `anthropic` (`claude-haiku-4-5`, only when `AI_PROVIDER=anthropic` + `ANTHROPIC_API_KEY` set).
- Request fields have Pydantic length bounds: job ID 1–128, job type 1–255, payload 1–65536 characters. The worker's input is already bounded to 64 KiB UTF-8 by submission validation.
- 18 pytest tests; ruff-clean. Runtime dependencies and development/test tooling are separate.

### Tests and CI

- `src/Tests/Jobs.Domain.Tests` (21), `src/Tests/Jobs.Application.Tests` (35, including HTTP AI-client contract and UTF-8 payload-limit regressions), `src/Tests/Platform.Api.Tests` (5 limiter cases) — 61 xUnit cases, plus 18 pytest cases: 79 total.
- `.github/workflows/ci.yml` — dotnet build+test, python ruff+pytest, and a full Compose startup/runtime-image scan job on push/PR to main. The container job forces the free stub provider.
- CI fails on NuGet/Python advisory findings and Trivy runtime-image findings at any severity. `.github/workflows/security.yml` scans full Git history and current files with Gitleaks. Actions/scanners are pinned, checkout credentials are not persisted, and workflow permissions are read-only.

### Local Development

- `docker compose up --build` runs the full stack: postgres, rabbitmq (+ management UI :15672), ai-service (:8000), api (:5200, auto-migrates), worker. Healthcheck-ordered startup.
- Infra-only + `dotnet run` flow still works (see README).
- Secrets from `.env` via DotNetEnv with `NoClobber()` so process environment variables win; `.env.example` includes the local .NET connection string. Compose constructs its container connection string from `POSTGRES_*`. Queue/DLQ names are wired into hosts, and worker startup waits for AI health. The API pins Microsoft.OpenApi 2.7.5 to fix the vulnerable transitive dependency.
- All published ports bind to loopback. Runtime images use digest-pinned Alpine bases with distro security updates. PostgreSQL's bundled Go helper is replaced with Alpine `su-exec`; the AI image excludes dev tools and removes installers after dependency installation. API/worker/AI run as non-root with read-only filesystems, dropped capabilities, and bounded temporary storage; all services disable privilege escalation.
- `.env` and private key material are excluded from Git and Docker build contexts. PostgreSQL Alpine/RabbitMQ 4 use fresh volumes; old volume declarations remain for preservation.

## Verified End-to-End (2026-07-06)

```text
Happy path: submit -> outbox -> dispatcher -> queue -> worker -> ai-service -> Completed with result.
Retry/DLQ: forceFailure -> 3 attempts -> Failed, attemptCount 2, 1 message in jobs.submitted.dlq.
Rate limit: 10x 201 then 429 + Retry-After for one tenant; other tenants unaffected.
Outbox: submit during RabbitMQ outage -> 201; broker restart -> dispatcher publishes -> job Completed.
```

## Not Implemented (intentionally — see completion-plan.md "Explicitly Out of Scope")

```text
Redis, auth, k6, cloud deployment, Kubernetes, frontend,
separate outbox relay deployable, OpenTelemetry/Jaeger tracing,
TTL delayed-retry queue, distributed rate limiting
```

## Delivery Guarantees and Limits

The original publish-after-save gap on submission is resolved: job + outbox row commit atomically, and the dispatcher marks a row processed only after a confirmed, routable publish. Broker downtime leaves the message staged for later dispatch. Confirmation followed by a crash before the processed-row update can cause duplicates.

The non-Pending guard handles sequential duplicate deliveries; it is not an atomic claim and does not make multiple workers safe against races. Run one API/dispatcher and one worker for the demonstrated behavior. Prefetch bounds unacknowledged deliveries; the current consumer processes callbacks serially.

Retry/DLQ publication remains separate from job-state persistence, so failure between the two operations can lose the retry/dead letter. Unexpected consumer exceptions are nacked without requeue. A crash or shutdown after persisting Processing can strand a job because recovery leases are not implemented. Consequently there is no end-to-end exactly-once or arbitrary-crash-recovery guarantee.

`attemptCount` counts scheduled job retries, not the HTTP client's in-process retries. Message retries republish immediately; HTTP retries use backoff/jitter on transient errors. Forced failures bypass the HTTP client and can exhaust quickly. `/health` is liveness only.
