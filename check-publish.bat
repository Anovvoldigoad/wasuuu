@echo off
setlocal
set "P=%~1"
if "%P%"=="" set "P=%~dp0publish\win-x64"
set ERR=0
for %%F in (NSC_ModManager_Winlator.exe CpkMaker.dll YACpkTool.exe vcredist_x86.exe) do (
  if not exist "%P%\%%F" (
    echo MISSING: %%F
    set ERR=1
  )
)
for %%D in (lib ParamFiles ModdingAPIFiles) do (
  if not exist "%P%\%%D\" (
    echo MISSING DIR: %%D
    set ERR=1
  )
)
if "%ERR%"=="0" (
  echo Publish preflight OK: %P%
) else (
  echo Publish preflight FAILED.
)
exit /b %ERR%
