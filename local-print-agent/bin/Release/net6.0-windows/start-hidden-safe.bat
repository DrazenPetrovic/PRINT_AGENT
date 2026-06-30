@echo off
setlocal

set PORT=4567
set EXE_PATH=%~dp0local-print-agent.exe

echo Checking port %PORT%...
for /f "tokens=5" %%a in ('netstat -ano ^| findstr /R /C:":%PORT% .*LISTENING"') do (
  set PID=%%a
  goto :killpid
)

goto :start

:killpid
echo Port %PORT% is in use by PID %PID%. Killing process...
taskkill /PID %PID% /F >nul 2>&1

:start
echo Starting Local Print Agent in hidden mode...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%EXE_PATH%' -WindowStyle Hidden"

echo Done.
exit /b 0
