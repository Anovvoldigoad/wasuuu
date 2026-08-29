# Mod Compatibility Test Checklist — Android v0.5.0

Use one mod at a time first. After each successful single-mod test, start combining mods to catch merge conflicts.

## Priority A — expected to work / regression coverage
1. Character add mod
   - roster entry / portrait
   - selection
   - battle load
   - jutsu / combo / awakening
   - custom D-Pad or condition-driven actions
   - ultimate jutsu start and finish
2. Stage add mod
   - stage appears in selection
   - icon / preview
   - battle loads
   - stage transitions if the mod uses them
3. Resource/UI mod
   - CPK builds
   - resource loads
   - no black screen
4. Localization-heavy character mod
   - English + at least one additional language
   - skill names / messages present

## Priority B — specifically stress UltimateStormAPI
1. Custom `conditionprmManager.xfbin`
2. Custom `specialCondParam.xfbin`
3. Custom `ougiAwakeningParam.xfbin`
4. MovesetPlus control enable/disable/change functions
5. StageMove / cinematic stage transitions
6. D-Pad actions, tilts, chakra-shuriken related controls

## Priority C — currently detected but semantic handlers are pending
Treat failures here as compiler feature gaps until proven otherwise:
- `model_config.ini` / costume-model semantics
- `TUJ_config.ini`
- `specialInteraction_config.ini`

## Test notes to record
For every mod, save:
- mod name/version
- compile report
- whether it installs alone
- whether battle loads
- broken feature and exact input/action
- whether issue reproduces after clean compile
- `console.log` / exported diagnostics when runtime-specific

## Clean retest sequence
1. Disable all other mods.
2. Clear Compiled Mods.
3. Enable only the target mod.
4. Compile Mods.
5. Fully close and relaunch the game.
6. Retest the exact feature.
