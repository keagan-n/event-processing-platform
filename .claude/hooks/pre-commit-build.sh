#!/usr/bin/env bash
set -euo pipefail

echo "Running build gate before commit..."
dotnet build src/Platform.sln --nologo --verbosity quiet 2>&1

if [ $? -ne 0 ]; then
  echo "BLOCKED: dotnet build failed. Fix build errors before committing."
  exit 1
fi

echo "Build passed."
