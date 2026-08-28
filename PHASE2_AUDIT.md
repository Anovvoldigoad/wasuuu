# Phase 2 audit - NSC Mod Manager Winlator Edition

## Port target

- Upstream source: NSC-ModManager 2.1.1.0
- UI target: WinForms
- Runtime target: .NET 8 / `net8.0-windows`
- RID: `win-x86`
- Self-contained: yes
- WPF build flag: no
- XAML files in port: 0
- ModernWpf DLLs in port: 0

## Compiler preservation check

Direct method-body comparison against the uploaded 2.1.1.0 source:

| Method | Similarity | Notes |
|---|---:|---|
| `bw_CompileModProcess_NSC` | 99.9361% | Original engine retained; end-of-compile game launch made optional |
| `bw_CompileModProcess_NS4` | 99.9321% | Original engine retained; end-of-compile game launch made optional |
| `CompileMods` | 100% | Identical |
| `CompileModsNS4` | 100% | Identical |
| `InstallModdingAPI` | 100% | Identical |

`CleanGameAssets` also retains the original implementation with one corrected dispatcher call so the `cleanMotionBlur` argument is not lost.

## Included backend surface

The port contains 89 C# source files, the original parameter editor/view-model code used by compilation, the original x86 `CpkMaker.dll`, the exact uploaded `XFBIN_LIB.dll`, NAudio dependencies required by NUS3BANK processing, original ParamFiles, ModdingAPIFiles, resources, and YACpkTool.

SHA-256 verification performed during porting confirmed the `lib/XFBIN_LIB.dll` in this package is byte-identical to the uploaded/original 2.1.1.0 XFBIN library.

## Wine-focused changes

- `Directory.GetCurrentDirectory()` is pinned to the application folder at startup because the original backend uses many relative paths.
- File/folder dialogs use WinForms rather than Windows API Code Pack shell dialogs.
- App settings no longer use WPF-generated settings infrastructure.
- INI profile calls no longer use `GetPrivateProfileString` / `WritePrivateProfileString`; the replacement is managed code.
- A compatibility dispatcher marshals legacy backend callbacks to the WinForms UI thread.
- Informational legacy message boxes are logged instead of spawning large numbers of modal dialogs during compilation; error/warning/choice dialogs remain visible.
- Game auto-launch is optional and defaults off for GameHub workflows.

## Remaining native dependency

`WinlatorEntry.cs` still calls `LoadLibrary("MSVCP100.dll")` only as a startup diagnostic. CPK functionality itself still relies on the original `CpkMaker.dll`, so VC++ 2010 x86 should remain installed in the prefix.

## Build validation limitation

No .NET SDK exists in the execution environment used for this port, and outbound network resolution is disabled. Therefore semantic compilation could not be run here. The project includes both a Windows build script and a Windows GitHub Actions workflow so `dotnet publish` is the next mandatory validation gate.

Before using Compile Mods on a real installation, keep a backup of the game data or use Steam verification as a recovery path.
