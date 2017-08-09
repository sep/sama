@echo off
cd /d %~dp0
rmdir /s /q publish\ 2>nul

echo Publishing...

dotnet publish sama -v quiet -c Release -r linux-arm /p:MvcRazorCompileOnPublish=false /nologo
if %ERRORLEVEL% NEQ 0 exit /b 1

dotnet publish sama -v quiet -c Release -r win-x64 /p:MvcRazorCompileOnPublish=true /nologo
if %ERRORLEVEL% NEQ 0 exit /b 1

move /y sama\bin\Release\netcoreapp2.0\linux-arm\publish . >nul
if %ERRORLEVEL% NEQ 0 exit /b 1

echo Copying precompiled views...

copy /y sama\bin\Release\netcoreapp2.0\win-x64\publish\sama.PrecompiledViews.dll publish\ /b >nul
if %ERRORLEVEL% NEQ 0 exit /b 1

echo Copying extra files...

copy /y rpi-prereqs\* publish\ >nul
if %ERRORLEVEL% NEQ 0 exit /b 1

echo Done!
echo *** Don't forget to create "appsettings.Production.json" in the publish folder.
