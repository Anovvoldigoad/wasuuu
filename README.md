# NSC Mod Manager Android

Native Android ARM64 port of the NSC ModManager workflow for Naruto x Boruto: Ultimate Ninja Storm Connections.

Current build: **Phase 2A / 0.2.0**.

No Winlator, Wine, WinForms, or WPF is used by this app. The Android UI and C# core run through .NET for Android, while CPK pack/extract runs as an ARM64 Android native library built with the NDK.

## Current features

- Install `.nsc`, `.ensc`, `.uns`, `.unse`, and legacy `.nus4` packages.
- Scan, enable/disable, and delete installed mods.
- Save/check a Storm Connections game directory.
- Native CPK pack + extract self-test.
- Install the original ModdingAPI payload into the selected game directory.
- Compile enabled CPK/resource mods into `cpk_assets.cpk` and `data_win32_modmanager.cpk`.
- Merge mod shaders into `nuccMaterial_dx11.nsh` with backup.
- Detect and validate parameter XFBIN files and report semantic work still pending.

See `PHASE2A_COMPILER.md` for exact compatibility boundaries.

## Build

Push the source to GitHub and run `.github/workflows/build-android.yml`. The artifact contains one file:

`NSC-ModManager-Android-Phase2A-Signed.apk`

The workflow verifies that both `lib/arm64-v8a/libcpkbridge.so` and the bundled ModdingAPI payload are present inside the signed APK before uploading it.
