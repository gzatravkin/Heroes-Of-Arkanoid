@echo off
title Heroes of Arkanoid II

echo Killing previous instances...
for /f "tokens=5" %%p in ('netstat -ano 2^>nul ^| findstr ":5080 " ^| findstr "LISTENING"') do taskkill /PID %%p /F >nul 2>&1
for /f "tokens=5" %%p in ('netstat -ano 2^>nul ^| findstr ":5175 " ^| findstr "LISTENING"') do taskkill /PID %%p /F >nul 2>&1
timeout /t 1 /nobreak >nul

echo Starting backend...
start "Backend" cmd /k "cd /d "%~dp0backend\Arkanoid.Server" && dotnet run"

echo Starting frontend...
start "Frontend" cmd /k "cd /d "%~dp0frontend" && npm run dev"

echo.
echo Both servers starting. Open http://localhost:5175 in your browser.
echo Close the Backend and Frontend windows to stop the game.
