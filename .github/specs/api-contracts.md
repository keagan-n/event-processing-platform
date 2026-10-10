# API Contracts

The API binds requests, calls application services, and maps results. Endpoints do not access EF Core or process jobs. Startup migration wiring is a host concern.

## GET /health

Returns `200 {"status":"healthy"}`. This is liveness only; it does not probe PostgreSQL or RabbitMQ.

## POST /jobs

```json
{
  "tenantId": "tenant-demo",
  "jobType": "analysis",
  "payload": "{\"text\":\"Quarterly portfolio risk review\"}"
}
```

| Field | Type | Rules |
| --- | --- | --- |
| tenantId | string | Required, trimmed, at most 255 characters |
| jobType | string | Required, trimmed, at most 255 characters |
| payload | string | Required, trimmed; at most 65536 UTF-8 bytes; may contain JSON or plain text |

`201 Created`, with `Location: /jobs/{jobId}/status`:

```json
{
  "jobId": "22222222-2222-2222-2222-222222222222",
  "tenantId": "tenant-demo",
  "jobType": "analysis",
  "status": "Pending",
  "createdUtc": "2026-10-09T20:00:00Z"
}
```

A successful submission stages the job and outbox row in one PostgreSQL transaction. Analysis is asynchronous; 201 does not mean analysis succeeded. RabbitMQ need not be available to accept a job once the API is running.

Validation failure returns `400` with `message: "Validation failed."` and an `errors` array. Missing fields, wrong JSON types, or malformed request JSON may be rejected by ASP.NET model binding before application validation. For a supplied tenant, the endpoint acquires a token before application validation; invalid submissions can consume that tenant's budget.

The complete HTTP request body is capped at 262144 bytes (256 KiB); exceeding it returns `413`. This allows JSON escaping overhead while application validation bounds the decoded payload.

A depleted tenant bucket returns `429`, `Retry-After` in seconds, and:

```json
{"message":"Rate limit exceeded.","tenantId":"tenant-demo"}
```

The default bucket has ten tokens, replenished every sixty seconds. Limits apply per trimmed tenant ID within one API process.

Tenant IDs are caller-supplied labels, not authenticated identities. The demo has no authorization; any local caller can submit jobs or retrieve a known job ID. Host ports bind to loopback; do not expose this API publicly.

## GET /jobs/{jobId}/status

`jobId` is a GUID. Returns `200`:

```json
{
  "jobId": "22222222-2222-2222-2222-222222222222",
  "tenantId": "tenant-demo",
  "jobType": "analysis",
  "status": "Completed",
  "createdUtc": "2026-10-09T20:00:00Z",
  "result": "Job of type 'analysis' analyzed...",
  "attemptCount": 0,
  "failureReason": null
}
```

Status is `Pending`, `Processing`, `Completed`, or `Failed`. `result` is set on completion. `failureReason` is set on final failure. `attemptCount` counts scheduled job retries, so `2` on final failure means three job attempts with the default configuration; it excludes the HTTP client's in-process retries.

Unknown IDs return `404 {"message":"Job not found.","jobId":"..."}`. A non-GUID route value does not match the endpoint and returns 404.

Request/submission models live in `src/Platform/API/Contracts/Jobs/`. The status response currently lives in `Jobs.Application/GetJobStatusResponse.cs` under the API contracts namespace; moving files is deferred.
