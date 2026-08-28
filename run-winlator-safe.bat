@echo off
cd /d "%~dp0"
set DOTNET_PROCESSOR_COUNT=4
set COMPlus_Thread_UseAllCpuGroups=0
set COMPlus_GCCpuGroup=0
start "" NSC_ModManager_Winlator.exe
