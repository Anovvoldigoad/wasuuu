# Phase 2A.1 UI Fix

Fixes the invisible Compile Mods button. `MakeButton()` intentionally uses width=0 + weight=1 for horizontal rows; Phase 2A accidentally added the compile button directly to the vertical root, making it zero pixels wide.

Changes:
- Compile Mods is now inside a horizontal action row.
- Added Install / Update ModdingAPI beside it.
- Application display version bumped to 0.2.1.
