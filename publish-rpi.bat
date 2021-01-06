@echo off
cd /d %~dp0
rmdir /s /q publish\ 2>nul

echo Publishing...

dotnet publish sama -c Release -r linux-arm -o "publish" /p:MvcRazorCompileOnPublish=true -p:PublishSingleFile=true --self-contained true
if %ERRORLEVEL% NEQ 0 exit /b 1

echo Copying extra files...

copy /y rpi-prereqs\* publish\ >nul
if %ERRORLEVEL% NEQ 0 exit /b 1

echo.
echo Done!
echo.
echo It is recommended to place the "publish" directory into "/opt/sama",
echo such that the "sama" executable is located in "/opt/sama/publish".
echo See the "sama-daemon.service" file for systemd integration.
pause
