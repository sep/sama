@echo off
cd /d %~dp0
rmdir /s /q publish\ 2>nul

echo Publishing...

dotnet publish sama -c Release -r linux-arm -o "publish" /p:MvcRazorCompileOnPublish=true /p:PublishSingleFile=true
if %ERRORLEVEL% NEQ 0 exit /b 1

echo Copying extra files...

copy /y rpi-prereqs\* publish\ >nul
if %ERRORLEVEL% NEQ 0 exit /b 1

echo Done!
