# Completion Plan (Resume-Ready Final Scope)

This document is the single authoritative plan for finishing this project.
It supersedes the milestone breakdown in `queue-ai-retry-rate-limit-roadmap.md`.
Where this plan conflicts with older scope notes, this plan wins.

## Purpose

Finish the platform so it works as a portfolio project for a backend /
distributed-systems engineer with real production experience. Every milestone
below earns its place by producing either:

1. A distributed-systems interview talking point (retries, DLQ, outbox,
   idempotency, backpressure, rate limiting), or
2. Proof of a skill the resume claims but does not yet demonstrate publicly
   (Python, automated testing, CI, LLM integration).

Anything that does not do one of those two things is out of scope. See
"Explicitly Out of Scope" at the bottom.

**Hard constraints:**

- **Zero paid services.** Everything runs locally via Docker Compose. CI is
  GitHub Actions (free for public repos). No cloud resources. The AI
  provider defaults to a free deterministic stub — no API key is ever
  required to run, demo, or test the system.
- **Realistic, not maximal.** Each pattern gets its simplest credible
  implementation. If a feature needs new infrastructure beyond what compose
  already runs, it does not go in.

## Build Status (2026-07-06)

**ALL SIX MILESTONES ARE IMPLEMENTED AND VERIFIED END-TO-END.** This plan is
now a historical record of scope decisions. See `current-state.md` for the
implemented system and the live-verified scenario list (happy path, retry ->
DLQ, per-tenant 429, outbox surviving a broker outage). 39 xUnit tests +
12 pytest tests green; full stack runs via `docker compose up --build`.

Two deliberate deviations from the milestone text below, chosen during
implementation (simpler, same talking points):

- Retry attempts are tracked ONLY in the `jobs.attempt_count` column (no
  message headers); a retry is a plain republish. The DLQ message carries an
  `x-failure-reason` header.
- `IJobProcessingService` returns a `JobProcessingResult` outcome instead of
  throwing, so the Worker maps outcomes to ack/republish/DLQ without ever
  touching job state (preserves the CLAUDE.md boundary).

## How to Execute This Plan

- Implement ONE milestone per session, in order. Say:
  "Implement Milestone N from .github/specs/completion-plan.md"
- Before coding, the agent must read `CLAUDE.md`, this file, and
  `.github/specs/current-state.md`, then inspect the actual files it will
  change (the repo is the source of truth; file listings below are guides,
  not gospel).
- Each milestone ends with: `dotnet build src/Platform.sln` passing, all
  tests passing, `current-state.md` updated, and a diff shown to the user.
- Do not commit unless the user approves the diff. Conventional commit
  messages (`feat:`, `test:`, `chore:`, `docs:`).
- Architecture boundaries in `CLAUDE.md` still apply: API is transport only,
  Application orchestrates, Domain owns rules, Infrastructure owns EF
  Core/RabbitMQ/HTTP clients, Worker calls Application services.

## Current State (as of this plan)

```text
POST /jobs -> validate -> persist (PostgreSQL) -> publish JobSubmittedMessage (RabbitMQ)
Worker consumes RabbitMQ -> IJobProcessingService -> Pending -> Processing -> Completed/Failed
Processing is a simulated 2-second Task.Delay.
GET /jobs/{jobId}/status reads persisted state.
ai-service/ is a non-functional stub (main.py has no FastAPI app).
No tests. No CI. No retries. No DLQ. No rate limiting. No outbox. No tracing.
```

---

## Milestone 1: Test Foundation + CI

**Why:** A public repo with no tests is a negative signal for an experienced
engineer. This also creates the safety net every later milestone builds on.

**Create:**

- `src/Tests/Jobs.Domain.Tests/Jobs.Domain.Tests.csproj` (xUnit)
- `src/Tests/Jobs.Application.Tests/Jobs.Application.Tests.csproj` (xUnit + NSubstitute)
- Add both projects to `src/Platform.sln`
- `.github/workflows/ci.yml`

**Domain tests (`JobTests.cs`):**

- `Create` produces `Pending` status with `CreatedUtc` set.
- `MarkProcessing` from `Pending` succeeds and sets `StartedUtc`.
- `MarkCompleted` from `Processing` succeeds and sets `CompletedUtc`.
- `MarkFailed` from `Processing` sets `FailedUtc` and `FailureReason`.
- Every invalid transition throws (e.g. `MarkCompleted` from `Pending`,
  `MarkProcessing` from `Completed`). Cover the full transition matrix.

**Application tests:**

- `JobSubmissionServiceTests`: validation failure returns errors and calls
  neither repository nor publisher; success persists BEFORE publishing
  (assert call order); trimming of inputs.
- `JobProcessingServiceTests` (mock `IJobRepository`): missing job throws;
  non-Pending job is skipped without state change; `forceFailure` payload
  marks Failed and rethrows; happy path marks Completed.

**CI (`.github/workflows/ci.yml`):**

- Trigger on push + PR to `main`.
- Job `dotnet`: setup .NET (match repo TFM, currently net10.0), restore,
  build `src/Platform.sln` with warnings-as-errors off, run `dotnet test`.
- Leave a clearly-marked placeholder job structure so Milestone 2 can add a
  `python` job without restructuring.

**Acceptance criteria:**

- `dotnet test src/Platform.sln` runs both test projects green locally.
- CI workflow YAML is valid and green on GitHub after push.
- README gets a CI badge (single-line change).

---

## Milestone 2: Python AI Service (FastAPI) + .NET Integration

**Why:** Resume lists Python with zero public evidence, and "LLM integration
behind a service boundary" is the single most marketable 2026 skill this
project can add. This replaces the fake `Task.Delay` with a real cross-service
call, which also unlocks realistic failure modes for Milestone 3.

### Python side (`ai-service/`)

Rewrite `app/main.py` as a real FastAPI app:

- `GET /health` -> `{"status": "healthy", "provider": "<active provider>"}`
- `POST /v1/analyze` with Pydantic models:
  - Request: `{ "job_id": str, "job_type": str, "payload": str }`
  - Response: `{ "job_id": str, "summary": str, "model": str, "latency_ms": int }`
  - Malformed request -> 422 (FastAPI default).

Provider abstraction in `app/providers/`:

- `base.py`: `AnalysisProvider` protocol with `async analyze(job_type, payload) -> (summary, model_name)`.
- `stub.py`: default. Deterministic summary derived from the payload (e.g.
  keyword extraction + length stats). No network. Small fixed latency
  (~200ms) so demos feel real. MUST be deterministic for tests.
- `anthropic_provider.py`: OPTIONAL path, used only when
  `AI_PROVIDER=anthropic` AND `ANTHROPIC_API_KEY` is set. Model
  `claude-haiku-4-5`. Single short completion: "Summarize this job payload
  in 2 sentences." Timeout 30s. The system is never demoed, tested, or
  documented as requiring this — the stub is the default and is free.
- Selection in `app/config.py` from env; default `stub` so the whole system
  runs with zero API keys and zero cost.

Housekeeping:

- Clean `requirements.txt`: `fastapi`, `uvicorn[standard]`, `pydantic`,
  `anthropic`, `python-dotenv`, plus dev deps `pytest`, `pytest-asyncio`,
  `httpx` (for TestClient), `ruff`. Remove unused `aiohttp`,
  `python-json-logger`, `black`.
- Fix `Dockerfile` HEALTHCHECK (it currently imports `requests`, which is not
  installed — use `urllib.request` instead).
- `ai-service/tests/`: pytest covering health endpoint, stub determinism,
  analyze happy path, 422 on bad request, provider selection logic.
- Add `ai-service` to `docker-compose.yml` (build from `./ai-service`, port
  `8000:8000`, env passthrough for `AI_PROVIDER` / `ANTHROPIC_API_KEY`).

### .NET side

- `Jobs.Application`: `IAiAnalysisClient` with
  `Task<AiAnalysisResult> AnalyzeAsync(AiAnalysisRequest request, CancellationToken ct)`.
  `AiAnalysisRequest(Guid JobId, string JobType, string Payload)` and
  `AiAnalysisResult(string Summary, string Model)` records live in
  `Jobs.Application` (they are application-level DTOs, not queue contracts).
- `Jobs.Infrastructure/AI/HttpAiAnalysisClient.cs`: typed `HttpClient`
  implementation calling `POST /v1/analyze`. `AiServiceOptions { BaseUrl }`
  from env `AI_SERVICE_BASE_URL` (default `http://localhost:8000`).
- Register the typed client in `Worker/Program.cs` (the API host does not
  need it).
- `JobProcessingService`: replace `Task.Delay` with the AI call; keep the
  `forceFailure` path exactly as-is. Store the returned summary.
- Domain change: add nullable `Result` string to `Job`; `MarkCompleted(string result)`
  now takes the result. Update tests.
- EF migration `AddJobResult` (use the migration commands in `CLAUDE.md`).
- `GET /jobs/{jobId}/status` response gains a nullable `result` field
  (update `GetJobStatusResult`, `GetJobStatusResponse`, `JobStatusService`).

### CI

- Add `python` job to `ci.yml`: setup Python 3.11, `pip install -r
  ai-service/requirements.txt`, `ruff check ai-service`, `pytest ai-service`.

**Acceptance criteria:**

- `docker compose up -d` brings up postgres + rabbitmq + ai-service; `curl
  localhost:8000/health` returns healthy.
- End-to-end manual check: POST /jobs -> job completes -> status shows a
  non-null `result` produced by the stub provider.
- All .NET and pytest tests green. CI green.

---

## Milestone 3: Retries with Backoff + Dead-Letter Queue

**Why:** "What happens when the AI service is down?" is the exact question an
interviewer asks about this architecture. This milestone is the answer.

**Two distinct layers — implement both and keep them clearly separated:**

1. **In-process transient retry (Polly):** wrap the `HttpAiAnalysisClient`
   HTTP call with Polly: 3 attempts, exponential backoff with jitter
   (e.g. 1s/2s/4s base), retry only on 5xx / timeouts / connection failures.
   Registered via `Microsoft.Extensions.Http.Resilience` or Polly directly on
   the typed client in Infrastructure. This handles blips.

2. **Message-level redelivery + DLQ:** handles sustained failure.
   - Add `AttemptCount` (int, default 0) to `Job` + domain method
     `MarkPendingForRetry()` allowing `Processing -> Pending` and
     incrementing `AttemptCount`. New allowed transition documented in
     domain tests. EF migration `AddJobAttemptCount`.
   - Worker consumer behavior on processing failure:
     - If `AttemptCount < MaxAttempts` (config, default 3): call
       `MarkPendingForRetry()`, persist, ack the original message, and
       republish `JobSubmittedMessage` to the main `jobs.submitted` queue
       with an incremented attempt header. Redelivery is immediate — the
       Polly backoff on the HTTP call already provides the delay between
       real attempts. (A TTL-based delayed-retry queue was considered and
       deliberately cut as over-engineering for this scale; note that as a
       "future work" line, don't build it.)
     - If attempts exhausted: `MarkFailed(reason)`, persist, ack, and
       publish the message (with failure reason header) to
       `jobs.submitted.dlq` (durable, no consumer — inspected via
       management UI).
   - All queue names and MaxAttempts configurable via `RabbitMqOptions` /
     env with the current defaults.
- Expose `attemptCount` in the status endpoint response.
- **Idempotency note (document in code + specs):** delivery is at-least-once;
  the consumer's existing "skip non-Pending jobs" check is the idempotency
  guard. Add an explicit application test proving a redelivered message for a
  `Completed` job is a no-op.

**Tests:** domain transition tests for `MarkPendingForRetry`; application
tests for retry-then-succeed, exhaust-then-fail paths (mock the AI client to
fail N times).

**Manual verification script (add to `manual-verification.md`):** stop the
ai-service container, submit a job, watch it retry 3 times and land in the
DLQ; restart ai-service, submit again, watch it succeed.

**Acceptance criteria:** all tests green; manual DLQ scenario verified;
`forceFailure` jobs also flow through retry then DLQ (they are deterministic
failures, which is fine — document that).

---

## Milestone 4: Per-Tenant Rate Limiting + Worker Backpressure

**Why:** Multi-tenant fairness and protecting a downstream dependency are
senior-level talking points, and "rate limiting" is already promised by the
repo's roadmap. No Redis (per existing constraint) — in-memory is correct at
this scale and easier to defend in an interview.

**API side (ingestion limit):**

- Use .NET's built-in `System.Threading.RateLimiting`:
  a singleton `PartitionedRateLimiter<string>` partitioned by `TenantId`,
  token bucket per tenant (defaults: 10 tokens, replenish 10 per minute;
  env-configurable).
- Because the partition key comes from the request BODY, do not use the
  ASP.NET middleware (it runs before binding). Apply it inside the
  `POST /jobs` endpoint (or an endpoint filter) after the request is bound:
  acquire 1 permit; on rejection return `429 Too Many Requests` with a
  `Retry-After` header and a JSON body `{ "message": "Rate limit exceeded.",
  "tenantId": ... }`.
- Keep the limiter policy in a small class (e.g.
  `Platform.Api/RateLimiting/TenantRateLimiter.cs`) so `Program.cs` stays
  thin. The empty `Modules/RateLimiting` projects stay untouched — using
  them for one policy class is ceremony without benefit. Note this decision
  in `current-state.md`.

**Worker side (backpressure on the AI service):**

- Set RabbitMQ channel prefetch (`BasicQos`) to a configurable value
  (default 4) in the consumer so the worker never floods the AI service.
  This is deliberate backpressure — document it as such.

**Tests:** unit-test the tenant limiter policy directly (tenant A exhausts
its bucket -> rejected; tenant B unaffected; bucket refills). Integration
verification manually via rapid curl loop.

**Acceptance criteria:** 11th rapid request for one tenant gets 429 with
Retry-After while another tenant still gets 201; tests green.

---

## Milestone 5: Transactional Outbox

**Why:** The repo's own docs currently say "publish-after-save can lose
messages; outbox solves this but is out of scope." Closing that documented
gap is the strongest narrative in the project: you shipped the tradeoff
knowingly, then eliminated it. This is THE classic event-driven-systems
interview question (atomic dual-write).

**Scope decision:** outbox moves from "deferred" to "final milestone" —
`CLAUDE.md` has been updated to reflect this. OutboxRelay as a separate
deployable stays out of scope; the dispatcher is a hosted service inside the
API process.

**Implementation:**

- `Jobs.Domain` stays untouched. Outbox is a persistence/messaging concern:
  - `Jobs.Infrastructure/Persistence/OutboxMessage.cs`: `Id (Guid)`,
    `OccurredUtc`, `Type` (string), `Payload` (JSON string),
    `ProcessedUtc (nullable)`, `Error (nullable string)`.
  - EF migration `AddOutboxMessages`.
- `Jobs.Application`: new `IOutboxWriter` abstraction
  (`Task EnqueueAsync(JobSubmittedMessage message, CancellationToken ct)` —
  it stages the row in the SAME DbContext/unit of work, so
  `SaveChangesAsync` commits job + outbox row in one transaction).
- `JobSubmissionService`: replace the direct `IJobQueuePublisher` call with
  `IOutboxWriter.EnqueueAsync` BEFORE `SaveChangesAsync`. After this
  milestone the API no longer publishes to RabbitMQ inline.
- `Platform.Api`: `OutboxDispatcherService` (BackgroundService): every 2s
  (configurable), load a batch (e.g. 20) of unprocessed messages ordered by
  `OccurredUtc`, publish each via the existing `IJobQueuePublisher`, set
  `ProcessedUtc`; on publish failure record `Error` and leave unprocessed
  for the next tick (natural retry). Use a DI scope per tick.
- At-least-once is now guaranteed end-to-end; the consumer idempotency guard
  from Milestone 3 absorbs duplicates. Document this pairing explicitly.

**Tests:** submission writes job + outbox row in one save (assert both
staged before `SaveChangesAsync`, and no direct publish); dispatcher
publishes pending rows and marks processed; dispatcher leaves rows
unprocessed when the publisher throws.

**Docs:** update the "Outbox Tradeoff" sections in `current-state.md` and
the roadmap from "accepted risk" to "resolved via transactional outbox",
keeping the original tradeoff text as history.

**Acceptance criteria:** kill RabbitMQ, POST a job (201, row saved), restart
RabbitMQ, watch the dispatcher publish and the job complete. Tests green.

---

## Milestone 6: Full Compose Demo + Structured Logging + README Polish

**Why:** Reviewers give a portfolio repo ~2 minutes. `git clone && docker
compose up` plus a README that explains the architecture decisions IS the
product.

**Structured logging (small, do first within this milestone):**

- Serilog structured JSON console logging in API + Worker, with jobId /
  tenantId / attemptCount enrichment on processing logs. That's it — no
  OpenTelemetry, no Jaeger, no metrics pipeline (deliberately cut; list
  distributed tracing as "future work" in the README instead).

**Containerize the .NET hosts:**

- `src/Platform/API/Dockerfile` and `src/Platform/Worker/Dockerfile`
  (multi-stage, `mcr.microsoft.com/dotnet/sdk` build -> `aspnet`/`runtime`).
- Add `api` and `worker` services to `docker-compose.yml` with proper env
  wiring and `depends_on` (postgres healthy, rabbitmq healthy). Apply EF
  migrations on API startup when `APPLY_MIGRATIONS=true` (compose sets it;
  local dev default stays manual).
- Result: `docker compose up` runs the ENTIRE system (postgres, rabbitmq,
  ai-service, api, worker).

**README rewrite (this is the sales page — spend effort here):**

1. One-paragraph pitch: what it is, what it demonstrates.
2. Badges: CI status, .NET version, Python version.
3. Mermaid architecture diagram: API -> Postgres, API -> outbox -> dispatcher
   -> RabbitMQ -> Worker -> ai-service, DLQ.
4. "Distributed-systems concepts demonstrated" table: transactional outbox,
   at-least-once + idempotent consumer, retry w/ exponential backoff, DLQ,
   per-tenant rate limiting, backpressure (prefetch) — each with a one-line
   "where in the code" link.
5. Quickstart: `docker compose up`, then a copy-pasteable curl demo script:
   submit, poll status, trigger forceFailure -> show DLQ, trigger rate limit
   -> show 429.
6. Design decisions section (or link to `docs/design-decisions.md`): why
   modular monolith, why outbox over 2PC, why in-memory rate limiting over
   Redis, why jobId-only messages (thin events), known limitations (single
   worker instance semantics, no auth by design).
7. Screenshot: RabbitMQ management UI showing the queues including the dlq.

**Also:** update `architecture.md`, `current-state.md`, and `index.md` to
final state; verify `manual-verification.md` covers every demo path.

**Acceptance criteria:** fresh clone on a clean machine: `docker compose up`
-> full demo works with only the README. All tests green in CI.

---

## Explicitly Out of Scope (decided, do not add)

| Item | Reason |
| --- | --- |
| Cloud deployment (AWS/Azure) | Resume already proves production AWS at CoStar; costs money, adds no new signal. `docker compose up` demo-ability is worth more. |
| Auth (OAuth2/JWT) | Already a headline resume bullet from real production work. Redundant here. |
| Redis | Existing constraint; in-memory rate limiting is defensible at this scale and a better interview answer. |
| k6 load tests | Resume already has k6 at 35k-loan scale in production. Redundant. |
| Kubernetes | Overkill for a modular monolith demo; compose is the right size. |
| Frontend/UI | Target role is backend. |
| Separate OutboxRelay deployable | Hosted-service dispatcher inside API is the right scale; a separate process adds ops surface without new concepts. |
| Multi-worker competing consumers / leader election | Mention as "known limitation / future work" in README instead of building. |
| OpenTelemetry / Jaeger tracing pipeline | Cut as over-engineering: structured logging with correlation fields (jobId, tenantId, attemptCount) tells the story; a tracing stack is heavy infra for a demo. Listed as future work in README. |
| Delayed-retry queue (TTL dead-letter pattern) | Cut as over-engineering: immediate message redelivery + Polly backoff on the HTTP call is the simplest credible retry story. Listed as future work. |
| Anthropic API as a requirement | Costs money. It exists only as an optional provider; the free deterministic stub is the default everywhere (demos, tests, CI, README). |

## Milestone Order Rationale

Tests first (safety net + immediate signal), AI service second (unlocks real
failure modes), retries third (needs real failures to be meaningful), rate
limiting fourth (independent, quick), outbox fifth (capstone reliability
story), demo + docs last (packages the finished system).
