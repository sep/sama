#!/bin/bash
cd $(dirname "$0")
export CLEANUP_SYSTEM_TESTS=true
dotnet test --filter "Name~Cleanup" --logger "console;verbosity=detailed" "$@"
