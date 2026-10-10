# Manual Verification

Use the public example configuration and free stub. The full-stack demo needs Docker Compose v2; the automated check additionally needs Python 3.9+ (standard library only). See [API contracts](api-contracts.md) for request/response details and [current state](current-state.md) for verified behavior and limits.

## Start and Verify the Full Demo

From the repository root, with ports 5200, 5432, 5672, 8000 and 15672 available:

```bash
docker compose --env-file .env.example up --build -d --wait
docker compose --env-file .env.example ps
python3 scripts/smoke_test.py
python3 scripts/smoke_test.py --broker-outage --env-file .env.example
```

The smoke check uses unique tenant IDs and verifies:

- Required/oversized fields or a payload exceeding 64 KiB UTF-8 return 400; a complete body exceeding 256 KiB returns 413; unknown jobs return 404.
- Submission returns 201 Pending and a usable status Location.
- PostgreSQL -> outbox -> RabbitMQ -> worker -> Python analysis -> Completed with result.
- Forced failure retries three times, returns Failed with attemptCount 2 and failureReason, and adds a DLQ message.
- Ten accepted submissions per tenant, then 429 with Retry-After; another tenant can still submit.
- Optional broker outage: stop RabbitMQ, accept a Pending job, restart RabbitMQ, then complete the staged job.
- Main queue eventually drains and a worker consumer remains attached.

Assumes the default three job attempts, ten rate-limit tokens and queue names. Custom token/queue names can be passed with `--tokens`, `--queue` and `--dlq`. The outage option stops the selected Compose project's broker and always attempts to restart it. It creates demo data; no database/queue purge occurs.

## Inspect Manually

```bash
curl -s http://localhost:5200/health
curl -i -X POST http://localhost:5200/jobs \
  -H "Content-Type: application/json" \
  -d '{"tenantId":"tenant-manual","jobType":"analysis","payload":"{\"text\":\"Quarterly portfolio risk review\"}"}'
# Poll the returned Location; expect Completed and a non-null result.
curl -s http://localhost:5200/jobs/<jobId>/status
```

RabbitMQ management UI: [localhost:15672](http://localhost:15672), guest/guest. The queues are `jobs.submitted` and `jobs.submitted.dlq`; the DLQ is declared on first terminal failure.

```bash
curl -s -u guest:guest http://localhost:15672/api/queues/%2f/jobs.submitted
curl -s -u guest:guest http://localhost:15672/api/queues/%2f/jobs.submitted.dlq

docker compose --env-file .env.example exec postgres \
  psql -U event_platform_user -d event_platform \
  -c 'SELECT id, tenant_id, status, attempt_count, result, failure_reason FROM jobs ORDER BY created_utc DESC LIMIT 5;'
docker compose --env-file .env.example exec postgres \
  psql -U event_platform_user -d event_platform \
  -c 'SELECT id, processed_utc, error FROM outbox_messages ORDER BY occurred_utc DESC LIMIT 5;'
```

Successful dispatch clears the outbox error and sets processed_utc. Failed rows retain an error and remain unprocessed for another dispatcher tick. Table/column names are lowercase; TenantId is a string.

## Development Build and Tests

```bash
dotnet build src/Platform.sln
dotnet test src/Platform.sln
python3.11 -m venv ai-service/.venv
ai-service/.venv/bin/python -m pip install -r ai-service/requirements-dev.txt
(cd ai-service && .venv/bin/python -m pytest)
ai-service/.venv/bin/ruff check ai-service scripts
```

Expected: .NET build succeeds; 61 xUnit and 18 pytest cases pass. See README for running .NET hosts locally with `.env`, EF migrations and infrastructure-only Compose. See [SECURITY.md](../../SECURITY.md) for secret/dependency/image audit commands and scope.

## Stop

```bash
docker compose --env-file .env.example down
```

Volumes are retained for later inspection. Do not run containerized and local API/worker hosts at the same time on the same ports/queues.
