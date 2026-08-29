# NSC Mod Manager Android — v0.5.0 / Phase 2D

Native Android ARM64 port of the NSC ModManager workflow for Naruto X Boruto Ultimate Ninja STORM CONNECTIONS. The manager itself runs natively on Android; GameHub/Winlator is only used to run the Windows game.

## Verified foundation
- Android app launches on-device.
- Native ARM64 CPK pack/extract bridge works on-device.
- Character/stage semantic merge can produce playable custom characters/stages.
- 15-language `messageInfo.bin.xfbin` localization merge works.
- UltimateStormAPI special parameter merge supports condition manager, special condition, and ougi/awakening files.

## Phase 2D: SC 1.70 runtime compatibility
`NSCApiConditionCompatFix_v1.dll` is bundled into the ModdingAPI payload and installed automatically whenever the app compiles mods or installs/updates ModdingAPI.

The runtime fix is generic: it derives condition counts from UltimateStormAPI runtime vectors and contains no character/mod IDs. It fixes an SC 1.70 condition-lookup signature mismatch found on the tested executable build.

Regression fixture verified after the fix:
- D-Pad custom condition action works.
- Stage-transition ultimate continuation completes correctly instead of leaving the opponent stuck.

The Android installer validates the compatibility DLL SHA-256 before and after installation.

## UI refresh
- New adaptive launcher icon.
- Dark panel-based visual style inspired by the desktop NSC Mod Manager aesthetic.
- Lightweight animated storm/chakra background rendered with Android Canvas (no GIF/video dependency).
- Runtime compatibility badge (`READY` / `INSTALLED`).
- Cleaner Game Setup, Mod Library, Compile & Runtime, Advanced Tools, and Status sections.

## Generic compiler direction
Community mods are treated as regression fixtures only. Core compilation is feature/format driven rather than character-name driven.

Current compiler warnings remain important: model/costume, Team Ultimate Jutsu config, and special-interaction config families are detected but their dedicated semantic handlers are still pending. Test those categories separately before calling them fully supported.

Build target: .NET 10 Android, `android-arm64`.

See:
- `PHASE2C_GENERIC_COMPAT.md`
- `PHASE2D_SC170_UI_RUNTIME_FIX.md`
- `MOD_COMPAT_TEST_CHECKLIST.md`
