#!/bin/sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
PROJECT_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)
ANDROID_DIR="$PROJECT_ROOT/android"

prepend_path() {
  if [ -d "$1" ]; then
    PATH="$1:$PATH"
  fi
}

prepend_path "/opt/homebrew/bin"
prepend_path "/usr/local/bin"
prepend_path "/usr/bin"
prepend_path "/bin"

if [ -z "${NODE_BINARY:-}" ]; then
  if command -v node >/dev/null 2>&1; then
    NODE_BINARY=$(command -v node)
  elif [ -x "/opt/homebrew/bin/node" ]; then
    NODE_BINARY="/opt/homebrew/bin/node"
  elif [ -x "/usr/local/bin/node" ]; then
    NODE_BINARY="/usr/local/bin/node"
  else
    echo "[AndroidGradle] Unable to find Node.js. Install Node or set NODE_BINARY before running this command." >&2
    exit 1
  fi
fi

export PATH
export NODE_BINARY

cd "$ANDROID_DIR"
exec ./gradlew --no-daemon "$@"
