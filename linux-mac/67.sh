#!/usr/bin/env bash
# Linux launcher for 67.py
cd "$(dirname "$0")" || exit 1
exec python3 67.py "$@"
