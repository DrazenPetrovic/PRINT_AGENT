@echo off
setlocal
set EXE_PATH=%~dp0local-print-agent.exe

sc query LocalPrintAgent >nul 2>&1
if %errorlevel%==0 (
  echo Service LocalPrintAgent already exists.
  pause
  exit /b 0
)

sc create LocalPrintAgent binPath= "%EXE_PATH%" start= auto DisplayName= "Local Print Agent"
if %errorlevel% neq 0 (
  echo Failed to create service. Run this script as Administrator.
  pause
  exit /b 1
)

sc description LocalPrintAgent "Local print API service for PDF/text/raw jobs on localhost."
sc start LocalPrintAgent

echo Service LocalPrintAgent installed and started.
pause
