@echo off
rem ---------------------------------------------------------------------------
rem  Builds TanbalType-<version>-setup.exe from a clean publish of the app.
rem
rem  Usage:  build-installer.bat            small setup.exe, .NET 8 downloaded
rem                                         on the target machine if missing
rem          build-installer.bat offline    .NET 8 packed into setup.exe
rem
rem  Needs the .NET 8 SDK and InstallShield 2025. No admin rights required.
rem ---------------------------------------------------------------------------
setlocal
cd /d "%~dp0.."

set VERSION=1.5.0
set IS_ROOT=C:\Program Files (x86)\InstallShield\2025
set ISCMDBLD=%IS_ROOT%\System\IsCmdBld.exe
set PS32=%SystemRoot%\SysWOW64\WindowsPowerShell\v1.0\powershell.exe

rem The .NET redistributable is fetched by New-TanbalTypeIsm.ps1 into
rem installer\prereq\. IsCmdBld -prqdownload is deliberately not used: it
rem writes into Program Files and so needs admin rights.
set BUNDLE=
if /i "%~1"=="offline" set BUNDLE=-BundleDotNet

rem Must match $setupSuffix in New-TanbalTypeIsm.ps1.
set SETUPNAME=TanbalType-%VERSION%-setup
if /i "%~1"=="offline" set SETUPNAME=TanbalType-%VERSION%-setup-offline

if not exist "%ISCMDBLD%" (
    echo ERROR: InstallShield command-line build not found:
    echo   %ISCMDBLD%
    echo Edit IS_ROOT in this script if InstallShield is installed elsewhere.
    exit /b 1
)

echo.
echo [1/3] Publishing the application...
rem ReadyToRun comes from the csproj; the .pdb is dropped because end users do
rem not need debug symbols.
dotnet publish TanbalType\TanbalType.csproj -c Release -r win-x64 --self-contained false ^
    -p:DebugType=none -o installer\payload --nologo
if errorlevel 1 exit /b 1

echo.
echo [2/3] Generating the InstallShield project...
rem 32-bit PowerShell: the InstallShield automation interface is a 32-bit COM server.
"%PS32%" -NoProfile -ExecutionPolicy Bypass -File "installer\New-TanbalTypeIsm.ps1" ^
    -ProductVersion %VERSION% %BUNDLE%
if errorlevel 1 exit /b 1

echo.
echo [3/3] Building the installer...
"%ISCMDBLD%" -p "installer\TanbalType.ism" -a "TanbalType" -r "SingleImage" -c COMP -e Y -y %VERSION%
if errorlevel 1 exit /b 1

echo.
echo Done:
echo   installer\TanbalType\TanbalType\SingleImage\DiskImages\DISK1\%SETUPNAME%.exe
echo.
pause
