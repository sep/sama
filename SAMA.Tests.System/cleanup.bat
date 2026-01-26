@echo off
cd /d %~dp0
set CLEANUP_SYSTEM_TESTS=true
dotnet test --filter "Name~Cleanup" --logger "console;verbosity=detailed" %*
