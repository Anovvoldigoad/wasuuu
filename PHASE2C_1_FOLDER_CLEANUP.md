# Phase 2C.1 — Folder Picker + Safe Cleanup

Version: 0.4.1

## Select Game Folder

The Android UI now launches `ACTION_OPEN_DOCUMENT_TREE`. For folders selected from Android's ExternalStorageProvider (Internal storage or SD card), the tree document ID is converted to the direct filesystem path required by the ARM64 native CPK bridge. Cloud/document-provider URIs are rejected because the compiler cannot pass `content://` streams to the native CPK path API.

## Clear Compiled Mods

`Clear Compiled Mods` removes NSC Mod Manager generated CPK/report files (including prior generated-CPK backups), restores `.nscmm_android.bak` files, removes compiler-managed overlay/parameter files, and reinstalls the bundled ModdingAPI payload to reset its parameter baseline. Installed mod packages remain in the user's mod storage and ModdingAPI remains installed.

## Remove ModdingAPI

`Remove ModdingAPI` first restores compiler backups and clears generated outputs. It then removes only the files/directories described by the bundled ModdingAPI payload plus compiler-owned files. It does **not** recursively delete the entire game `moddingapi` directory, so unrelated user files are preserved when possible. As in the desktop manager, `d3dcompiler_47_o.dll` is restored to `d3dcompiler_47.dll` when present.

## Managed-file manifest

New compiles record overwritten/created loose files in `moddingapi/mods/base_game/nsc_android_managed_files.txt`. This lets cleanup delete files that did not exist before compilation while restoring files that had a `.nscmm_android.bak` backup. Older Phase 2B/2C installs remain compatible through the unique backup suffix recovery path.
