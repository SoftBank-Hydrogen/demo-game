@echo off
cd /d "%~dp0"
where node >nul 2>nul
if errorlevel 1 (
  echo Please install Node.js 22.13 or newer, then run this file again.
  pause
  exit /b 1
)
call npm --prefix game\server ci
if errorlevel 1 (
  pause
  exit /b 1
)
node Tools\run-demo.cjs
pause
