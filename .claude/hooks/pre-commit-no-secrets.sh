#!/usr/bin/env bash
set -euo pipefail

STAGED=$(git diff --cached --name-only 2>/dev/null || true)

if echo "$STAGED" | grep -qE '(^|/)\.env$'; then
  echo "BLOCKED: .env is staged for commit. Only .env.example should be committed."
  echo "Run: git reset HEAD .env"
  exit 1
fi

if echo "$STAGED" | grep -qiE '(credentials\.json|secrets\.json|\.pem$|\.key$)'; then
  echo "BLOCKED: Potential secrets file staged for commit."
  echo "Staged files matching filter:"
  echo "$STAGED" | grep -iE '(credentials\.json|secrets\.json|\.pem$|\.key$)'
  exit 1
fi

echo "No secrets detected in staged files."
