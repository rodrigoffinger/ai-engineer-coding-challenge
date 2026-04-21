@echo off
setlocal

echo ============================================
echo  Grocery Store SOP Assistant - Dev Launcher
echo ============================================
echo.

REM ---- Frontend: install deps ----
echo [1/3] Installing frontend dependencies...
cd /d "%~dp0frontend"
call npm install --silent
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: npm install failed.
    pause
    exit /b 1
)
echo       Done.
echo.

REM ---- Backend: start in a new window ----
echo [2/3] Starting backend (http://localhost:5181)...
start "SOP Backend" cmd /k "cd /d "%~dp0backend\src\Api" && dotnet run --launch-profile http"
timeout /t 2 /nobreak >nul

REM ---- Frontend: start dev server in a new window ----
echo [3/3] Starting frontend dev server (http://localhost:5173)...
start "SOP Frontend" cmd /k "cd /d "%~dp0frontend" && npm run dev"

echo.
echo Both services are starting in separate windows.
echo   Backend  ^> http://localhost:5181
echo   Frontend ^> http://localhost:5173
echo.
echo Close this window at any time - the services run in their own windows.
pause
