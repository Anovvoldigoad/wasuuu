
## Phase 2.1 build fix

The first real GitHub Actions compile exposed namespace collisions between the legacy WPF-compatible API names and WinForms implicit imports. Phase 2.1 explicitly qualifies those APIs and pins the CI build to .NET SDK 8.0.424. See `BUILD_FIX_2_1.md`.

# NSC Mod Manager 2.1.1.0 - Winlator Edition (Phase 2)

This is a compatibility port of TheLeonX/NSC-ModManager 2.1.1.0 aimed at Wine/Winlator/GameHub.

## What changed

- UI: WPF / ModernWpf -> Windows Forms.
- Framework: .NET 10 -> .NET 8 (`net8.0-windows`).
- Architecture: remains x86 because `CpkMaker.dll` and the original manager are x86.
- Publish target: `win-x86`, self-contained, non-single-file.
- No XAML is compiled and no WPF/ModernWpf assembly is referenced.
- Legacy WPF-shaped backend properties are handled by tiny in-project compatibility types; they do not load PresentationFramework/ModernWpf.
- Folder/file dialogs are mapped to WinForms dialogs.
- Settings are stored in `winlator_settings.json` beside the executable.
- Legacy kernel32 INI profile APIs were replaced with a managed INI implementation.
- The compatibility dispatcher is bound to the WinForms UI thread for safer background compiler callbacks under Wine.
- Automatic game launch after compile is OFF by default. This is useful when the game itself is launched through GameHub.

## Compile engine status

Phase 2 includes the original 2.1.1.0 compiler backend, not the reduced Lite placeholder.

Preserved paths include:

- Storm Connections compile pipeline (`bw_CompileModProcess_NSC`)
- Storm 4 compile pipeline (`bw_CompileModProcess_NS4`)
- character / costume / stage / Team Ultimate Jutsu parameter merging
- XFBIN reading/writing through the original `XFBIN_LIB.dll`
- CPK operations through the original x86 `CpkMaker.dll`
- ModdingAPI installation
- YACpkTool and original ParamFiles / ModdingAPIFiles assets

The two main compiler bodies are >99.9% text-identical to 2.1.1.0. The intentional difference is that launching the game at the end is conditional on `Launch after compile`, rather than unconditional.

## Build

Requirements on the build PC:

- Windows 10/11
- .NET 8 SDK

Run:

```bat
build-win-x86.bat
```

or:

```bat
dotnet publish NSC-ModManager.Winlator.csproj -c Release -r win-x86 --self-contained true -o publish\win-x86
```

A GitHub Actions workflow is included at `.github/workflows/build-winlator.yml`.

## Winlator usage

1. Copy the entire published `win-x86` folder into the Wine prefix / a writable game-tools directory.
2. Install `vcredist_x86.exe` (VC++ 2010 x86) in that same prefix.
3. Launch `NSC_ModManager_Winlator.exe`.
4. Select the Storm Connections game folder and mod-manager folder.
5. Keep `Launch after compile` unchecked if the game is launched from GameHub.
6. Install/enable mods and use `Compile Mods`.
7. After compilation finishes, launch the game from GameHub if desired.

Do not copy only the EXE. Keep `CpkMaker.dll`, `lib`, `ParamFiles`, `ModdingAPIFiles`, `Resources`, and `YACpkTool.exe` beside it.

## Important validation note

The port has been statically audited in the current environment, including direct comparison of the core compiler bodies against 2.1.1.0. This environment does not contain a .NET SDK and has no network access, so a real `dotnet publish` could not be executed here. Use the included batch file or GitHub workflow as the build gate before testing game assets. Test with backups first.

See `PHASE2_AUDIT.md` for details.
