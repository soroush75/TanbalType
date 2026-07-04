@echo off
setlocal
cd /d "%~dp0"
dotnet build TanbalType.sln -c Release
if errorlevel 1 exit /b 1
echo.
echo Build OK:
echo   TanbalType\bin\Release\net8.0-windows\TanbalType.exe
pause
