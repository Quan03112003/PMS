@echo off
cd /d "%~dp0frontend"
if not exist "node_modules" npm install
if errorlevel 1 goto failed
npm start
:failed
pause
