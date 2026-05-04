# API Contracts

## Purpose

This document defines the MVP HTTP API.

The API should remain thin. It should:

- Receive HTTP requests
- Bind request bodies
- Map requests to application commands
- Call application services
- Return HTTP responses

The API should not:

- Create domain entities directly
- Use EF Core directly
- Contain job processing logic
- Contain business rules

## Endpoint: Health Check

### Request

```http
GET /health
```

### Response

```http
200 OK
```

```json
{
  "status": "healthy"
}
```

## Endpoint: Submit Job

### Request

```http
POST /jobs
```

### Request Body

```json
{
  "tenantId": "11111111-1111-1111-1111-111111111111",
  "jobType": "ai.summary",
  "payload": "{\"text\":\"Summarize this content\"}"
}
```

### Request Fields

| Field | Type | Required | Description |
|---|---|---:|---|
| tenantId | guid | yes | Tenant identifier |
| jobType | string | yes | Type of job |
| payload | string | yes | Job payload as string |

### Response

```http
201 Created
```

```json
{
  "jobId": "22222222-2222-2222-2222-222222222222",
  "tenantId": "11111111-1111-1111-1111-111111111111",
  "jobType": "ai.summary",
  "status": "Pending",
  "createdUtc": "2026-04-05T20:00:00Z"
}
```

## Endpoint: Get Job Status

### Request

```http
GET /jobs/{jobId}/status
```

### Found Response

```http
200 OK
```

```json
{
  "jobId": "22222222-2222-2222-2222-222222222222",
  "status": "Pending",
  "createdUtc": "2026-04-05T20:00:00Z",
  "startedUtc": null,
  "completedUtc": null,
  "failedUtc": null,
  "failureReason": null
}
```

### Possible Status Values

```text
Pending
Processing
Completed
Failed
```

### Not Found Response

```http
404 Not Found
```

```json
{
  "message": "Job not found.",
  "jobId": "22222222-2222-2222-2222-222222222222"
}
```

## API Contract File Locations

API request/response models should live under:

```text
src/Platform/API/Contracts/Jobs/
```

Expected contracts:

```text
SubmitJobRequest.cs
SubmitJobResponse.cs
GetJobStatusResponse.cs
```

## API Rules

- API maps `SubmitJobRequest` to `SubmitJobCommand`.
- API maps application results to API response contracts.
- API returns `201 Created` when a job is submitted.
- API returns `404 Not Found` when a job status is requested for a missing job.
- API does not directly instantiate `Job`.
- API does not use `DbContext`.
- API does not process jobs.