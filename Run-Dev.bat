@echo off
setlocal

cd /d "%~dp0"

echo Starting Debaters BackEnd...
start "Debaters BackEnd" cmd /k "cd /d "%~dp0backend" && dotnet run --project BackEnd.csproj --urls http://localhost:5008"

echo Starting Debaters FrontEnd...
start "Debaters FrontEnd" cmd /k "cd /d "%~dp0frontend" && npm run dev"

echo.
echo Both apps are launching in separate windows.
echo Close this window when ready.
pause
