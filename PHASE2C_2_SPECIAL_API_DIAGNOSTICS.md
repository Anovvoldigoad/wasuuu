# Phase 2C.2 — UltimateStormAPI verification and diagnostics

Phase 2C.2 keeps compilation feature-driven. No character/stage names from regression fixtures are hardcoded in Core.

## Added

- Post-compile verification for `conditionprmManager.xfbin`, `specialCondParam.xfbin`, and `ougiAwakeningParam.xfbin`.
- Report counters: `Special ModdingAPI post-compile checks: passed/expected`.
- `specialCondParam`, `partnerSlotParam`, and `susanooCondParam` now remap every complete 0x20-byte record rather than only the first record.
- `Toggle API Debug` updates UltimateStormAPI `enable_debug` + `enable_console` settings.
- `Export API Log` copies `console.log` / `imgui_log.txt` to `<mod storage>/logs` for sharing/debugging.
- CI verifies that the bundled UltimateStormAPI runtime contains D-Pad, StageMove, and OugiAwakening runtime markers.

## Why this matters

Advanced community movesets can depend on both compiled resources and runtime UltimateStormAPI hooks. A successful CPK/XFBIN compile does not by itself prove that D-Pad special conditions, stage transitions, or ougi/awakening runtime hooks were loaded. Phase 2C.2 separates these layers in the report.
