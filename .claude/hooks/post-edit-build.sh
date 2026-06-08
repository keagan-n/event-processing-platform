#!/usr/bin/env bash
set -euo pipefail

# Only run if the edited file is a .cs file
# The CLAUDE_TOOL_INPUT env var contains the tool input as JSON
# For Edit/Write tools, check if the file_path ends in .cs
FILE_PATH=$(echo "$CLAUDE_TOOL_INPUT" 2>/dev/null | grep -o '"file_path":"[^"]*"' | head -1 | cut -d'"' -f4 || true)

if [ -z "$FILE_PATH" ]; then
  exit 0
fi

case "$FILE_PATH" in
  *.cs)
    echo "C# file changed — running build check..."
    dotnet build src/Platform.sln --nologo --verbosity quiet 2>&1
    if [ $? -ne 0 ]; then
      echo "WARNING: Build broken after editing $FILE_PATH"
      exit 1
    fi
    echo "Build still passes."
    ;;
  *)
    exit 0
    ;;
esac
