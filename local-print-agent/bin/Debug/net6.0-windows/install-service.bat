@echo off
setlocal
set EXE_PATH=%~dp0local-print-agent.exe
set SERVICE_NAME=LocalPrintAgent
set PORT=4567

echo Releasing possible process lock on port %PORT%...
for /f "tokens=5" %%a in ('netstat -ano ^| findstr /R /C:":%PORT% .*LISTENING"') do (
  echo Port %PORT% is used by PID %%a. Killing...
  taskkill /PID %%a /F >nul 2>&1
)

taskkill /IM local-print-agent.exe /F >nul 2>&1

sc query %SERVICE_NAME% >nul 2>&1
if %errorlevel%==0 (
  echo Service %SERVICE_NAME% already exists. Restarting it...
  sc stop %SERVICE_NAME% >nul 2>&1
  sc start %SERVICE_NAME%
  if %errorlevel% neq 0 (
    echo Service start failed. Check Event Viewer logs for %SERVICE_NAME%.
    pause
    exit /b 1
  )
  echo Service %SERVICE_NAME% restarted successfully.
  pause
  exit /b 0
)

sc create %SERVICE_NAME% binPath= "%EXE_PATH%" start= auto DisplayName= "Local Print Agent"
if %errorlevel% neq 0 (
  echo Failed to create service. Run this script as Administrator.
  pause
  exit /b 1
)

sc description %SERVICE_NAME% "Local print API service for PDF/text/raw jobs on localhost."
sc start %SERVICE_NAME%
if %errorlevel% neq 0 (
  echo Service start failed. Check Event Viewer logs for %SERVICE_NAME%.
  pause
  exit /b 1
)

echo Service %SERVICE_NAME% installed and started.
pause
