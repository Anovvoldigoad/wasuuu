# Phase 1.3 publish fix

This revision addresses all 19 C# errors reported by the 2026-08-28 GitHub Actions publish log.

- `TextView.TextIsSelectable` property -> `SetTextIsSelectable(true)` for current .NET Android bindings.
- `OpenableColumns` -> `Android.Provider.IOpenableColumns`.
- Rewrote `Core/Nus4Extractor.cs` as a portable Android implementation with no references to the Windows `Program`, `PRMEditorViewModel`, `MessageInfoS4ViewModel`, `MessageInfoModel`, custom `BinaryReader.crc32`, or `Debug` dependencies.
- Preserves NUS4 metadata, CPKs, character/stage structure, shaders, data files, stage text and BGM metadata.
- PRM compatibility rewrites and stage message XFBIN generation are intentionally deferred to Android compiler Phase 2 instead of being stubbed.
- Cleaned `AndroidPrefs` nullable editor handling.
