@echo off
sc stop LocalPrintAgent >nul 2>&1
sc delete LocalPrintAgent
if %errorlevel% neq 0 (
  echo Failed to delete service. Run this script as Administrator.
  pause
  exit /b 1
)

echo Service LocalPrintAgent removed.
pause
