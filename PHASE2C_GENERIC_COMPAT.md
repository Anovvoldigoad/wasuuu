# Phase 2C — Generic compatibility + localization

Phase 2C does not special-case any character or mod. The Tobi/Madara package used during development is only a regression fixture.

## Generic feature discovery
Each enabled mod is scanned for feature families before compilation:
- `character_config.ini`
- `stage_config.ini`
- `model_config.ini`
- `TUJ_config.ini`
- `specialInteraction_config.ini`
- `messageInfo.bin.xfbin`
- character PRM (`*prm.bin.xfbin`)
- known ModdingAPI parameter XFBINs
- CPK archives
- HLSL shaders

The compile report records the feature map for every mod. Unsupported feature families are reported as warnings instead of being silently ignored.

## Localization
Phase 2C adds a portable `messageInfo.bin.xfbin` parser/merger/serializer with no WPF dependency. It follows the NSC ModManager 2.1.1.0 language behavior and processes one target language at a time to avoid holding all 15 large vanilla message tables in memory on Android.

NSC target languages:
`arae chi eng esmx fre ger idid ita kokr pol por rus spa thth zhcn`

NS4 sources are mapped to NSC targets with the same fallback policy used by the desktop compiler: `zhcn` falls back to `chi`, while NSC-only targets without an NS4 source fall back to `eng`.

## Diagnostics
The report now includes:
- localization source files, target-language merges, appended entries and generated outputs;
- special ModdingAPI parameter files merged;
- character PRM files detected and damage-effect remaps performed;
- per-mod feature map/details.

## Still pending
The following config families are detected and warned about, but are not yet semantically compiled:
- model/costume (`model_config.ini`)
- Team Ultimate Jutsu (`TUJ_config.ini`)
- special interaction (`specialInteraction_config.ini`)

This source is statically validated in-repo. The Android C# build is still verified by GitHub Actions, not by the local workspace.
