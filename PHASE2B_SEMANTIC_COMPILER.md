# Phase 2B — Semantic Character/Stage Compiler

Version: 0.3.0

This phase ports the NSC ModManager 2.1.1.0 character/stage parameter editors and compiler logic to the Android build.

## Active in this phase
- Character config parsing and semantic parameter merge.
- Stage config parsing and semantic StageInfo merge.
- NS4 -> NSC conversion paths used by the original character compiler.
- Rebuild of merged XFBIN parameter outputs.
- Native ARM64 repack of `param_files.cpk`.
- Generated ModdingAPI parameter files under `moddingapi/param/NSC`.
- Character/stage UI/resource overlays required by the original compiler path.
- Existing Phase 2A resource, shader and CPK compilation.

## Deliberately deferred
- `model_config.ini` / costume-mod compiler.
- Team Ultimate Jutsu / special interaction manager compiler.
- Full message/localization merge.

The compiler reports deferred inputs instead of silently replacing vanilla parameter files.

## Safety
Generated CPKs and overwritten runtime overlay files are backed up once with `.nscmm_android.bak` before replacement. Work is staged in app cache and native CPKs are packed before installation into the game folder.
