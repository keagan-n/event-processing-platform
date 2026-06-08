---
description: "Rules for editing markdown specs and documentation files. Use when modifying .md files in .github/specs/ or project root docs."
applyTo: "**/*.md"
---
# Documentation Rules

## Source of Truth

- `.github/specs/current-state.md` controls what is implemented.
- `.github/specs/queue-ai-retry-rate-limit-roadmap.md` controls active roadmap.
- These two files override older historical specs if there is a conflict.

## When Editing Specs

- Do not duplicate source-of-truth content into other files. Point to it.
- Do not make stale claims about project state. Check `current-state.md` first.
- If runtime behavior changes, update `current-state.md` to match.
- Do not delete or rename spec files without explicit approval and reference checks.
- Run `grep -r "filename" .github/ .claude/ CLAUDE.md AGENTS.md` before renaming any spec.

## Avoid

- Copying roadmap items into multiple files.
- Describing Worker as "polling PostgreSQL" — Worker consumes RabbitMQ.
- Referencing Redis, OutboxRelay, or outbox pattern as active.
- Listing completed milestones as upcoming.
