# Event Processing Platform — Development Guide

## Project Foundation

This is a **multi-tenant, event-driven, rate-limited processing platform** with **mandatory AI integration**. Tenancy is not bolted on—it shapes every layer.

- **Core responsibility**: Process events reliably at scale with per-tenant rate limits, audit trails, and AI-powered decision logic
- **Multi-tenancy model**: Tenant ID is always required; isolated quotas, separate logical event streams
- **Rate limiting**: Hard limits enforced at event ingestion and AI invocation; graceful degradation when approached
- **AI integration**: Not optional—baked into workflows; guide AI decisions through feature extraction and result validation

## Architecture Principles

1. **Event-first design**: Store the event, then decide what to do. Immutable append-only event log is the source of truth.
2. **Tenant isolation at the query level**: WHERE tenant_id = ? on every query; no cross-tenant visibility in results.
3. **Rate limits as a first-class concern**: Quotas are checked early; backpressure/rejection logged and metered.
4. **Async by default**: Event handlers dispatch asynchronously; synchronous responses only when latency permits.
5. **Fail safely**: Unprocessed events remain in queue; failed AI decisions trigger alerts and manual review paths.

## Code Standards

### Language-Specific
See: `.github/instructions/csharp-dotnet.instructions.md`, `.github/instructions/python.instructions.md`

### Database & Persistence
See: `.github/instructions/database.instructions.md`

### AI & ML Integration
See: `.github/instructions/ai-integration.instructions.md`

## Build, Test, Dependencies

**Install** (macOS):
```bash
# .NET 8+
dotnet --version

# Python 3.11+ (for Python components)
python3 --version

# PostgreSQL 15+, Redis 7+
brew services list | grep postgres
redis-cli ping

# k6 for load testing
brew install k6
```

**Test**:
```bash
# Unit tests
dotnet test --configuration Release

# Integration tests (requires local DB/Redis)
dotnet test --filter Category=Integration

# Load testing
k6 run tests/load/*.js
```

**Build**:
```bash
dotnet build --configuration Release
```

## Common Patterns

### Adding a New Event Type
1. Define schema in `Domain/Events/<DomainArea>Events.cs`
2. Add migration: `dotnet ef migrations add Add<EventName>Event`
3. Create handler in `Application/EventHandlers/<EventType>Handler.cs`
4. Handler must extract tenant_id, check rate limits, log outcome
5. If AI-enriched: use AI integration module; catch failures gracefully

### Adding a New Rate-Limited Operation
1. Register quota key in `Infrastructure/RateLimiting/QuotaRegistry.cs`
2. Check quota before operation: `await _rateLimiter.CheckQuotaAsync(tenantId, operationKey)`
3. If denied, return 429 with Retry-After header
4. Log both accepts and rejections for dashboard visibility

### Adding AI to a Workflow
1. See `.github/instructions/ai-integration.instructions.md`
2. Extract features from event (structured inputs, not raw text)
3. Call AI via client with tenant context
4. Validate result against business rules before action
5. If confidence < threshold or error: log, queue for manual review, do not fail the event

## Local Development Notes (macOS)

- Use `brew services` for Postgres/Redis; confirm they start on boot
- For AWS work: `aws configure --profile dev-local` with test credentials
- Kill hanging processes: `lsof -i :5432` (Postgres), `lsof -i :6379` (Redis)
- Python venv: `python3 -m venv venv && source venv/bin/activate`

## Documentation References

- **Architecture**: See `docs/architecture.md` (to be created)
- **Event schema**: See `docs/events.md`
- **Rate limiting**: See `Infrastructure/RateLimiting/README.md`
- **AI integration**: See `docs/ai-integration.md`
- **Database**: See `docs/database.md`

## What Copilot Should Do

✓ Generate event handlers with tenant checks and error handling
✓ Create database migrations and indexes for multi-tenant access patterns
✓ Write Python scripts for AI feature extraction and result processing
✓ Suggest rate limit checks at entry points
✓ Propose structured logging with tenant context
✗ Do NOT generate single-tenant code without tenant_id parameter
✗ Do NOT skip rate limit checks "to make things simple"
✗ Do NOT call AI APIs synchronously in hot paths
