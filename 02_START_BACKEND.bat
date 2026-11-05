@echo off
cd /d "%~dp0backend\HotelPms.Api"
dotnet restore
if errorlevel 1 goto failed
dotnet run --launch-profile HotelPms.Api
:failed
pause
