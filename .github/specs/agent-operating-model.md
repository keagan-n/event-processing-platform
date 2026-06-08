# Agent Operating Model

Rules for AI agents working in this repository. Applies to Claude Code, GitHub Copilot, Codex, ChatGPT, and any other AI tool.

## Before Editing

1. Inspect relevant files.
2. Ask for or run `git status`.
3. State current behavior of files you will change.
4. List exact files to create or modify.
5. Explain the proposed change.
6. Wait if the change is ambiguous.

## During Editing

- Keep changes scoped and small.
- Do not make broad cleanup changes without grep/reference checks first.
- Do not delete or rename files without explicit approval.
- Do not introduce deferred features (see constraints below).

## After Editing

1. Run `dotnet build src/Platform.sln`.
2. Show `git diff --stat` and `git diff`.
3. Provide manual test steps if applicable.
4. Do not commit or push unless explicitly told.

## Commit Rules

- Use semantic commit messages: `feat:`, `fix:`, `refactor:`, `chore:`, `docs:`, `test:`.
- Keep commits small and focused.
- Show the diff before any commit.
- Never commit `.env`, credentials, or secrets.

## Hard Constraints

- No Redis unless explicitly redesigned.
- No OutboxRelay or outbox pattern.
- Worker consumes RabbitMQ messages. Worker must not poll PostgreSQL.
- Future AI integration goes through Python `ai-service`, not direct OpenAI SDK from .NET.
- PostgreSQL remains system of record.
- RabbitMQ is the active queue.
- Rate limiting must not be Redis-backed unless explicitly redesigned.
- DB save + RabbitMQ publish is intentionally non-atomic (outbox out of scope).

## When Runtime Behavior Changes

If you change runtime behavior, update `.github/specs/current-state.md` to match.

## Documentation Rules

- `current-state.md` and `queue-ai-retry-rate-limit-roadmap.md` are the source of truth.
- Do not duplicate their content into other files. Point to them.
- Do not delete or rename specs without explicit approval and reference checks.
