# Specs Index

Documentation map for agents and contributors.

## Source of Truth

These two files control current project reality. They override older historical specs if there is a conflict:

- **current-state.md** — What is implemented right now.
- **queue-ai-retry-rate-limit-roadmap.md** — Active roadmap and implementation order.

## Architecture and Design

- **architecture.md** — Architecture style, layer responsibilities, dependency rules.
- **jobs-module.md** — Jobs module domain, application services, repository, lifecycle.
- **persistence.md** — PostgreSQL/EF Core persistence behavior.
- **api-contracts.md** — HTTP API endpoints, request/response contracts.

## Agent Guidance

- **agent-operating-model.md** — How AI agents should work in this repo.
- **agent-workflow.md** — Spec-driven development process, prompt templates, deferred features.
- **manual-verification.md** — Local end-to-end verification checklist.

## Historical (Reference Only)

These exist for context but may be stale. `current-state.md` and the roadmap take precedence:

- **development-plan.md** — Original MVP milestone plan (milestones 1-4 are complete).
- **project-scope.md** — Original scope document.
- **worker-processing.md** — Original polling Worker spec (Worker now uses RabbitMQ consumer).
- **worker-processing-milestone.md** — Completed milestone record.

## Claude Code Working Memory

`.claude/context/` contains a local mirror of key specs for Claude Code quick access.
If `.claude/context/` conflicts with `.github/specs/`, `.github/specs/` wins.

See `.claude/context/README.md` for details.
