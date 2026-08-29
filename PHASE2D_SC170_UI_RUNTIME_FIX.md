# Phase 2D — SC 1.70 Runtime Compatibility + Android UI refresh

## Runtime fix
The Android payload now bundles `NSCApiConditionCompatFix_v1.dll` under
`moddingapi/mods/base_game/` and installs it automatically on both **Compile Mods**
and **Install / Update ModdingAPI**.

The fix is generic and derives condition counts from UltimateStormAPI runtime vectors.
It does not hardcode Tobi, characode 281, Izanagi, Kamui, or a specific mod ID.

Tested regression fixture: Tobi (Madara Uchiha) 1.2 on the user's SC 1.70 executable.
Confirmed after the fix:
- Right D-Pad Izanagi works.
- Kamui ultimate completes and the opponent no longer remains stuck.

The Android installer verifies the bundled DLL SHA-256 before installation and verifies
the installed copy afterward. Old temporary diagnostic DLLs created during the debugging
session are removed by exact known filenames so they cannot interfere with normal play.

## UI refresh
- New adaptive launcher icon (original Android design, inspired by the dark/white NSC tool aesthetic).
- Dark panel-based layout closer to the desktop NSC Mod Manager feel.
- Animated lightweight "storm/chakra" background drawn natively with Canvas; no GIF dependency.
- Runtime-fix badge showing READY / INSTALLED.
- Cleaner sections: Game Setup, Mod Library, Compile & Runtime, Advanced Tools, Status.
- App version bumped to 0.5.0 (version code 16).
