---
description: "Cross-agent safety rules to prevent destructive or out-of-scope changes. Applies to all AI agents."
applyTo: "**/*"
---
# Agent Safety Rules

## Do Not

- Commit or push unless explicitly requested.
- Delete files without explicit approval.
- Rename spec files without updating all references (grep first).
- Make broad cleanup changes without grep/reference checks.
- Stage `.env`, credentials, secrets, `.pem`, or `.key` files.
- Reintroduce Redis or Redis-backed rate limiting.
- Reintroduce OutboxRelay or outbox pattern.
- Reintroduce PostgreSQL polling in the Worker.
- Call OpenAI SDK directly from .NET — future AI goes through Python `ai-service`.
- Skip `CancellationToken` on async I/O methods.
- Put business logic in API endpoints.
- Let Domain reference EF Core, ASP.NET, or infrastructure.

## Always

- Inspect files before editing.
- Run `dotnet build src/Platform.sln` after code changes.
- Show diff before commit.
- Use semantic commit messages.
- Keep changes small and scoped.
- Update `current-state.md` if runtime behavior changes.
