#!/bin/bash
# Wrapper script to run document-parser.jar with proper environment
export PATH="/opt/homebrew/bin:$PATH"
export LANG=en_US.UTF-8
export LC_ALL=en_US.UTF-8
export JAVA_TOOL_OPTIONS="-Dfile.encoding=UTF-8"
exec java -Xmx256m -Xms128m -Dfile.encoding=UTF-8 -jar "$(dirname "$0")/document-parser.jar" "$@"
