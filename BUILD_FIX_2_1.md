# Phase 2.1 Build Fix

Based on the first GitHub Actions build log (2026-08-28).

## Fixed

- Resolved CS0104 collisions caused by WinForms implicit namespaces vs compatibility shims:
  - Microsoft.Win32.OpenFileDialog
  - Microsoft.Win32.SaveFileDialog
  - System.Windows.MessageBox
  - System.Windows.Application
  - System.Windows.Clipboard
- Fixed `View.Details` resolving to `NSC_ModManager.View.Details`; it is now explicitly `System.Windows.Forms.View.Details`.
- Removed ineffective `Prefer32Bit` property. Runtime remains explicitly `win-x86` / `PlatformTarget=x86`.
- Added `global.json` to force the .NET 8.0.424 SDK. The first Actions run installed .NET 8 but actually published through SDK 10.0.400 because that newer SDK was also present on the runner.
- Pinned Actions runner to `windows-2022` and setup-dotnet to `8.0.424` for a more reproducible build.

## Notes

The first build restored successfully. Its compile failures were overwhelmingly symbol ambiguity errors introduced by the WPF-to-WinForms compatibility layer, rather than failures inside the NSC/NS4 compiler algorithm.

The old local `System.Reactive.dll` / `DynamicData.dll` may still produce a WindowsBase version warning. It was non-fatal in the first run and is intentionally left unchanged in this fix to avoid changing compiler behavior at the same time as resolving the blocking C# errors.

If the next build exposes a second layer of errors, use that log as the next compiler-driven port pass.
