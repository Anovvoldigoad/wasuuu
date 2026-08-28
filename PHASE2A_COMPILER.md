# NSC Mod Manager Android — Phase 2A Compiler

Version: 0.2.0 / Android ARM64 / .NET 10 Android

## What Phase 2A actually compiles

This phase activates the first real Android-native compile pipeline:

1. Reads enabled mods from the configured mod storage directory.
2. Copies non-parameter `.xfbin`, `.acb`, and `.awb` files from `Resources/Files` into a merged staging tree.
3. Finds `.cpk` files inside enabled mods, extracts them through the native ARM64 CPK bridge, merges their extracted trees, then repacks them as `cpk_assets.cpk`.
4. Packs merged loose resources as `data_win32_modmanager.cpk`.
5. Writes the same CPK `.info` IDs used by the desktop compiler (`0x20` for `cpk_assets`, `0x21` for `data_win32_modmanager`).
6. Merges unique `.hlsl` shader blobs into the game's `data/system/nuccMaterial_dx11.nsh`, with a one-time `.nscmm_android.bak` backup.
7. Installs/updates the original ModdingAPI payload into the selected game directory.
8. Writes `moddingapi/mods/base_game/nsc_android_compile_report.txt`.

All generated CPKs are built in the Android app cache first. They are copied into the game only after native CPK packing succeeds.

## XFBIN behavior in this phase

Parameter-like XFBIN files are detected using the same parameter keyword family used by the original ModManager. Their NUCC header is validated (magic/version/header fields) before they are reported.

They are **not** installed as whole-file overrides in Phase 2A. The original desktop compiler semantically merges records such as characode, player settings, roster, stage info, message info, damage/effect parameters, etc. Replacing the entire file with one mod's copy would be unsafe and would break multi-mod/vanilla data.

`character_config.ini`, `stage_config.ini`, and `model_config.ini` are also detected and included in the compile report as pending semantic work.

Existing `param_files.cpk` and `resources_modmanager.cpk` are deliberately left untouched by Phase 2A.

## Phase 2B target

Port the original parameter editor/merger algorithms into platform-neutral C# and generate `param_files.cpk` natively on Android. This includes character/roster/stage/message parameter merge rather than a last-wins file copy.

## Device test order

1. Install the signed Phase 2A APK.
2. Grant Storage Access.
3. Set the real Storm Connections root path and press `Save / Check Path`.
4. Press `CPK Pack + Extract Self-Test`. It must report `OK`.
5. Install/enable a resource/CPK mod.
6. Press `Compile Mods`.
7. Inspect `moddingapi/mods/base_game/nsc_android_compile_report.txt` if warnings are reported.

Do not use Phase 2A as proof of full character/roster parameter compatibility yet; that is Phase 2B.
