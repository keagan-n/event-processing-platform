# Agent Workflow

This repository uses spec-driven development.

AI agents must not generate large unreviewed changes.

## Required Agent Process

For every coding task, agents should:

1. Inspect relevant files.
2. Restate the current state.
3. List exact files to create or modify.
4. Explain the proposed change.
5. Keep the implementation small.
6. Build after the change.
7. Provide manual test steps.
8. Avoid deferred features unless requested.

## Good Agent Prompt Template

```text
You are working in a C#/.NET modular monolith repo.

Before editing, inspect:
- AGENTS.md
- CLAUDE.md
- .github/specs/current-state.md
- .github/specs/development-plan.md
- the files directly relevant to the task

Task:
<describe task>

Constraints:
- Keep changes small.
- Do not add deferred features.
- Respect architecture boundaries.
- List files you will change before editing.
- After editing, provide build and test commands.
```

## Deferred Features

Agents must not add these by default:

```text
Redis
outbox relay
outbox pattern
dead-letter queues
auth
cloud deployment
k6 tests
advanced observability
advanced multi-tenancy
```

Roadmap order to follow by default:

```text
1) Queue-based dispatch
2) Python AI service integration
3) Retries after AI integration
4) Rate limiting after queue + AI + retries
```

Notes:

- Rate limiting remains in roadmap but is not active now.
- Future rate limiting should not be Redis-backed unless explicitly redesigned.
- Queue/outbox tradeoff must be documented when queue work is proposed:
  DB save + queue publish is simpler but not atomic; a publish failure after save can lose dispatch.

## Build Command

```bash
dotnet build src/Platform.sln
```

## API Run Command

```bash
dotnet run --project src/Platform/API/Platform.Api.csproj
```

## Docker Command

```bash
docker compose up -d
```

## Testing Commands

Manual tests are done with Postman:

```text
GET  http://localhost:5200/health
POST http://localhost:5200/jobs
GET  http://localhost:5200/jobs/{jobId}/status
```
