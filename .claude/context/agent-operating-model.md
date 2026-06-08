# Agent Operating Model (Working Memory)

Claude Code operating rules. See `.github/specs/agent-operating-model.md` for full spec.

## Before Editing

- Inspect relevant files.
- Ask for or run `git status`.
- State current behavior.
- List files to change.
- Explain why.

## During Editing

- Keep changes scoped and small.
- Do not delete or rename files without explicit approval.
- Do not make broad cleanup without grep/reference checks.

## After Editing

- Run `dotnet build src/Platform.sln`.
- Show `git diff --stat` and `git diff`.
- Provide manual test steps.

## Commit Rules

- Do not commit or push unless explicitly told.
- Show diff before any commit.
- Use semantic commits: `feat:`, `fix:`, `refactor:`, `chore:`, `docs:`, `test:`.
- Never commit `.env` or secrets.

## If Runtime Behavior Changes

Update `.github/specs/current-state.md` to match.
