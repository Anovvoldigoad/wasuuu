# Android port audit — Phase 2C

## Native Android foundation
- Native Android UI (`net10.0-android`, ARM64).
- Android document/storage flow and SharedPreferences.
- Native ARM64 CPK pack/extract through `libcpkbridge.so` built from `darkruss48/cpk-toolkit`.
- No Winlator/Wine requirement.

## Semantic compiler
The character/stage merge path currently reuses portable portions of the original NSC ModManager 2.1.1.0 binary editors/serializers behind a headless compatibility layer. Desktop preview-image I/O is disabled on Android.

Phase 2C adds a new portable `messageInfo.bin.xfbin` localization core rather than importing the old WPF message editor. It processes one NSC target language at a time to keep memory pressure lower on Android.

## Generic compatibility policy
Development mods are regression fixtures only. Core code must not hardcode character names/codes or fixture-specific stage IDs. CI checks for fixture-token leakage and the runtime report contains a per-mod feature map.

Community convention filenames and extensions are discovered case-insensitively because archives are usually authored on Windows but compiled on Android/Linux.

## Known pending semantic handlers
- model/costume config
- Team Ultimate Jutsu config
- special-interaction config

These are detected and surfaced as warnings rather than silently treated as supported.
