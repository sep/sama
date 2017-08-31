@echo off
cd /d %~dp0
rmdir /s /q publish\ 2>nul

echo Publishing...

dotnet publish sama -v quiet -c Release -r linux-arm -o "bin\publish\publish" /p:MvcRazorCompileOnPublish=false /nologo
if %ERRORLEVEL% NEQ 0 exit /b 1

dotnet publish sama -v quiet -c Release -o "bin\publish\tmp" /p:MvcRazorCompileOnPublish=true /p:RuntimeIdentifier= /nologo
if %ERRORLEVEL% NEQ 0 exit /b 1

echo Copying precompiled views...

copy /y sama\bin\publish\tmp\sama.PrecompiledViews.dll sama\bin\publish\publish\ /b >nul
if %ERRORLEVEL% NEQ 0 exit /b 1

echo Moving published project to destination...

move /y sama\bin\publish\publish . >nul
if %ERRORLEVEL% NEQ 0 exit /b 1

echo Copying extra files...

copy /y rpi-prereqs\* publish\ >nul
if %ERRORLEVEL% NEQ 0 exit /b 1

echo Done!
