# Winlator SAF Direct Mode — v0.5.3

## Why this exists

A normal Android application cannot open another application's raw private path such as `/data/user/0/<winlator-package>/...` by filesystem APIs. However, a Winlator build may intentionally expose its Wine filesystem through Android's Storage Access Framework (`DocumentsProvider`). If that provider appears in the system folder picker, the user can explicitly grant access to a selected game directory.

NSC Mod Manager v0.5.3 uses that granted tree URI directly. No root, Shizuku, or helper EXE is required for this mode.

## Selection flow

1. Tap **Select Folder / Winlator**.
2. In Android's picker, choose the Winlator provider.
3. Navigate through the exported Wine filesystem to the Storm Connections directory containing `NSUNSC.exe`.
4. Tap **Use this folder / Allow**.
5. The app calls `TakePersistableUriPermission` and stores the tree URI.
6. A temporary create/write/delete probe confirms the provider is writable enough for compiler output.

If the picker returns an ExternalStorageProvider folder that resolves to an accessible real path, the app keeps using the faster direct-path backend instead.

## Compile architecture

```text
Winlator C: through DocumentsProvider
        |
        | only required source baselines
        v
NSC Mod Manager app cache
        |
        | semantic XFBIN + native ARM64 CPK compile
        v
local game shadow
        |
        | stream changed outputs through ContentResolver
        v
Winlator C: through persisted SAF tree
```

The entire PC game is never copied to cache. Current semantic compilation only needs the game shader baseline when a shader merge is requested; NSC parameter and localization baselines are already bundled with the APK.

## Safety

- Paths are relative to the selected tree only; `..` traversal is rejected.
- Existing compiler backups use `.nscmm_android.bak`.
- Existing backups are not replaced with newer modded copies.
- Managed-file manifest cleanup is preserved.
- Binary writes prefer `rwt`/`wt`. If a provider only offers plain `w`, the destination is recreated first so shorter binaries cannot leave stale trailing bytes.
- Condition compatibility DLL hash is verified after install/compile.
- Diagnostic/tracer DLLs owned by this project are cleaned from the base-game plugin folder.

## Supported through SAF in v0.5.3

- Validate game folder
- Compile Mods
- Install / Update API
- Clear Compiled
- Remove API
- Compile error log
- Runtime-fix installed badge

Advanced runtime probe/debug/export tools remain direct-path-only for now. They fail explicitly instead of silently trying to use an empty filesystem path.

## Root mode

`ROOT / WINLATOR C:` is retained as an optional advanced backend for rooted devices. It is not required when Winlator's DocumentsProvider exposes the desired C: game folder.
