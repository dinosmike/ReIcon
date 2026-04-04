@echo off
setlocal

set "ROOT=%~dp0"
set "PUBLISH=%ROOT%publish"

cd /d "%ROOT%ReIcon"

taskkill /IM ReIcon.exe /F >nul 2>&1
ping -n 2 127.0.0.1 >nul

dotnet publish "%ROOT%ReIcon\ReIcon.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DeleteExistingFiles=true --nologo -v q -o "%PUBLISH%"
if errorlevel 1 (
    echo BUILD FAILED
    pause
    exit /b 1
)

set "EXE=%PUBLISH%\ReIcon.exe"

if not exist "%EXE%" (
    echo ERROR: %EXE% not found
    pause
    exit /b 1
)

start "" "%EXE%"
