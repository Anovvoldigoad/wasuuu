# Phase 2.2 runtime fix

Targets the Winlator-Ludashi ARM64EC startup failure from the x86 build.

Changes:
- Main app moved from win-x86 to win-x64 self-contained .NET 8.
- Removed compile-time/in-process reference to CpkMaker.dll.
- CpkMaker.dll remains in the publish folder for external x86 YACpkTool.exe.
- Removed unused legacy in-process YaCpkTool implementation that forced the project to load CpkMaker.
- Forced HighDpiMode.DpiUnaware and WinForms AutoScaleMode.None.
- Added winlator_startup.log checkpoints and unhandled exception diagnostics.
- Added run-winlator-safe.bat to cap DOTNET_PROCESSOR_COUNT at 4.

Test order:
1. Run NSC_ModManager_Winlator.exe directly.
2. If it fails, send winlator_startup.log from the same directory.
3. Also try run-winlator-safe.bat.

CPK helper remains x86:
- YACpkTool.exe: .NET Framework 4.6 x86
- CpkMaker.dll: mixed-mode x86, VC++ 2010 x86
VC++ 2010 x86 may still be needed for CPK repack, but is no longer needed just to start the x64 UI.
