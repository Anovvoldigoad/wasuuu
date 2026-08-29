# NSC Mod Manager Android — v0.5.3 / Phase 2E Winlator SAF Direct

Native Android ARM64 port of the NSC ModManager workflow for Naruto X Boruto Ultimate Ninja STORM CONNECTIONS. The manager itself runs natively on Android; GameHub/Winlator is only used to run the Windows game.

## Verified foundation
- Android app launches on-device.
- Native ARM64 CPK pack/extract bridge works on-device.
- Character/stage semantic merge can produce playable custom characters/stages.
- 15-language `messageInfo.bin.xfbin` localization merge works.
- UltimateStormAPI special parameter merge supports condition manager, special condition, and ougi/awakening files.
- SC 1.70 runtime condition compatibility fix restores custom-condition execution on the tested build.

## v0.5.3: Winlator SAF Direct (no root)
Winlator can expose its private Wine filesystem through an Android `DocumentsProvider`. When the user picks the game directory from that provider with `ACTION_OPEN_DOCUMENT_TREE`, Android grants NSC Mod Manager access to the selected subtree even though the underlying raw `/data/user/0/...` path remains sandboxed.

The app now supports three game-target backends:

1. **Direct Android path** — regular `/storage/emulated/0/...` or accessible SD-card game folders.
2. **Winlator SAF Direct** — no-root direct read/write through the selected document-tree URI. This is the intended mode when the game is stored inside Winlator C:.
3. **ROOT / Winlator C:** — legacy/advanced direct raw-path bridge for rooted devices.

SAF mode is not a fake picker-only implementation. It is wired through the actual compile/runtime maintenance pipeline:
- persisted tree URI permission (`TakePersistableUriPermission`);
- provider read/write/delete probe when selecting a folder;
- minimal baseline streaming from C: to local compiler cache (currently the shader baseline when needed);
- semantic compile and native CPK creation stay in app-local cache;
- generated CPK/XFBIN/ModdingAPI outputs stream back to C: through `ContentResolver`;
- `.nscmm_android.bak` backup policy is preserved;
- managed-file cleanup works through SAF;
- Install/Update ModdingAPI works through SAF;
- Clear Compiled works through SAF;
- Remove ModdingAPI works through SAF;
- `NSCApiConditionCompatFix_v2.dll` is SHA-256 verified after SAF installation.

The full PC game is never mirrored into Android cache.

## SC 1.70 runtime compatibility
`NSCApiConditionCompatFix_v2.dll` remains bundled in the ModdingAPI payload. The proven condition-lookup fix is generic and derives condition counts from UltimateStormAPI runtime vectors; no character/mod IDs are hardcoded.

Regression fixture after the condition fix:
- custom D-Pad condition action works;
- stage-transition ultimate continuation completes instead of leaving the opponent stuck.

## UI
- Adaptive launcher icon.
- Dark panel-based visual style inspired by the desktop NSC Mod Manager aesthetic.
- Lightweight native storm/chakra background animation.
- Runtime compatibility/storage-mode badge.
- `Select Folder / Winlator` automatically chooses direct-path mode when a real Android path is available, otherwise uses the returned SAF tree directly.
- `ROOT / WINLATOR C:` remains available for rooted devices.

## Generic compiler direction
Community mods are regression fixtures only. Core compilation is feature/format driven rather than character-name driven. Unsupported semantic feature families should be reported rather than silently discarded.

Build target: .NET 10 Android, `android-arm64`.

See:
- `WINLATOR_SAF_DIRECT_MODE.md`
- `PHASE2C_GENERIC_COMPAT.md`
- `PHASE2D_SC170_UI_RUNTIME_FIX.md`
- `MOD_COMPAT_TEST_CHECKLIST.md`
