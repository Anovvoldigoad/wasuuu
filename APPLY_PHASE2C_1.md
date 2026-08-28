# Apply Phase 2C.1

Apply this patch on top of the Phase 2C source.

New user-facing features:
- `Select Game Folder` uses the Android folder picker and resolves Internal storage/SD-card selections to the direct path required by the native CPK bridge.
- `Clear Compiled Mods` clears generated mod output, restores `.nscmm_android.bak` files, and keeps/reset ModdingAPI.
- `Remove ModdingAPI` restores backed-up game files and removes the bundled ModdingAPI payload while preserving unrelated files where possible. `d3dcompiler_47_o.dll` is restored as `d3dcompiler_47.dll`, matching the desktop manager behavior.

Expected GitHub Actions artifact:
`NSC-ModManager-Android-Phase2C.1-arm64-net10`

Expected APK:
`NSC-ModManager-Android-Phase2C.1-Signed.apk`
