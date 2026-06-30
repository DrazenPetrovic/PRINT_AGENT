@echo off
setlocal EnableExtensions EnableDelayedExpansion
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
  call :remove_existing_service
  if %errorlevel% neq 0 (
    pause
    exit /b 1
  )
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
echo.
echo Current service status:
sc query %SERVICE_NAME%
pause
exit /b 0

:remove_existing_service
echo Service %SERVICE_NAME% already exists. Removing old service...
sc stop %SERVICE_NAME% >nul 2>&1
sc delete %SERVICE_NAME% >nul 2>&1

set /a WAIT_COUNT=0
:wait_delete
sc query %SERVICE_NAME% >nul 2>&1
if %errorlevel% neq 0 goto :removed

set /a WAIT_COUNT+=1
if !WAIT_COUNT! geq 10 (
  echo Failed to remove old service %SERVICE_NAME%.
  exit /b 1
)

timeout /t 1 /nobreak >nul
goto :wait_delete

:removed
echo Old service removed successfully.
exit /b 0
