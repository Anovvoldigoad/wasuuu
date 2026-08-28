@echo off
setlocal
cd /d "%~dp0"

echo Building NSC Mod Manager - Winlator Edition...
dotnet --info >nul 2>&1
if errorlevel 1 (
  echo ERROR: .NET 8 SDK or newer was not found.
  echo Install the .NET 8 SDK on Windows, then run this file again.
  exit /b 1
)

dotnet restore "NSC-ModManager.Winlator.csproj"
if errorlevel 1 exit /b 1

dotnet publish "NSC-ModManager.Winlator.csproj" -c Release -r win-x86 --self-contained true --no-restore -o "publish\win-x86"
if errorlevel 1 exit /b 1

call "%~dp0check-publish.bat" "%CD%\publish\win-x86"
if errorlevel 1 exit /b 1

echo.
echo Done: %CD%\publish\win-x86
endlocal
