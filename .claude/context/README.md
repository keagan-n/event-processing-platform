# Claude Code Working Memory

`.claude/context/` is Claude Code's quick local working memory — a lightweight mirror of key project facts for fast context loading.

## Relationship to `.github/specs/`

`.github/specs/` is the canonical repo-wide source of truth. It is read by all agents (Claude, Copilot, Codex, ChatGPT).

`.claude/context/` is a Claude Code convenience layer. It mirrors essential facts from specs so Claude Code can load context without reading multiple files.

**If `.claude/context/` conflicts with `.github/specs/`, `.github/specs/` wins.**

## Files

- `current-state.md` — Mirror of current runtime behavior.
- `roadmap.md` — Mirror of active implementation order.
- `architecture-rules.md` — Hard architecture constraints.
- `agent-operating-model.md` — Claude Code operating rules.
- `manual-verification.md` — Local verification checklist.

## Keeping Aligned

When `.github/specs/current-state.md` or the roadmap changes, update the corresponding file here. These are short summaries, not full copies — keep them concise.
