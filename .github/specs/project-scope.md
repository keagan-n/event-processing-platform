# Project Scope

## Goal

This project is a simplified job-processing platform built with C#/.NET, PostgreSQL, and a modular monolith architecture.

The MVP goal is to support the simplest meaningful backend processing loop:

1. A client submits a job.
2. The job is stored in PostgreSQL.
3. A worker finds pending jobs.
4. The worker processes the job.
5. The job status is updated.
6. The client can query the job status.

## MVP User Flows

1. Submit a Job

A client sends:

```http
POST /jobs

The API accepts the request, maps it to an application command, creates a domain Job, and stores it in PostgreSQL with Pending status.

2. Query Job Status

A client sends:

GET /jobs/{jobId}/status

The API returns the current status of the job from PostgreSQL.

3. Process Jobs

A background worker polls PostgreSQL for pending jobs.

When it finds a job, it:

Marks the job Processing.
Simulates work.
Marks the job Completed.

If processing fails, it marks the job Failed.

In Scope for MVP

The MVP includes:

GET /health
POST /jobs
PostgreSQL persistence for jobs
GET /jobs/{jobId}/status
Worker polling PostgreSQL
Job status transitions
Basic failure status support
Out of Scope for MVP

The MVP does not include:

Redis
Rate limiting
Python AI service calls
Queues
Outbox pattern
Retries
Dead-letter queue
Authentication
Authorization
Distributed tracing
k6 load testing
Advanced multi-tenancy
Advanced observability
Future Phases

Future versions may add:

Redis-based rate limiting
Python AI job execution
Outbox table
Queue-based worker processing
Retry policies
Dead-letter queue
Metrics and tracing
k6 load testing
Tenant isolation and quotas
MVP Definition of Done

The MVP is done when:

POST /jobs persists a job to PostgreSQL.
The job starts with Pending status.
GET /jobs/{jobId}/status returns the persisted job status.
The worker can find pending jobs.
The worker updates status from Pending to Processing.
The worker updates status from Processing to Completed.
Failed processing marks the job as Failed.
Restarting the API does not lose submitted jobs.