@echo off
setlocal
cd /d "%~dp0"
dotnet publish NSC-ModManager.Winlator.csproj -c Release -r win-x64 --self-contained true -o publish\win-x64
if errorlevel 1 exit /b %errorlevel%
call check-publish.bat publish\win-x64
