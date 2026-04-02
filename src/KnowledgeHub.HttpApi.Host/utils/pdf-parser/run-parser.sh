#!/bin/bash
# Wrapper script to run document-parser.jar with proper environment
export PATH="/opt/homebrew/bin:$PATH"
exec java -Xmx256m -Xms128m -jar "$(dirname "$0")/document-parser.jar" "$@"
