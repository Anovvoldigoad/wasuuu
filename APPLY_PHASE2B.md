> Historical Phase 2B guide. For the current build use `APPLY_PHASE2C.md`.

# Apply Phase 2B over Phase 2A.1

This patch is intended for a repository already containing **Phase 2A.1**.

Overwrite/copy every path in the patch ZIP into the repository root. Do not delete the existing `Assets/Payload/moddingapi_payload.zip`; Phase 2B continues to use it.

Important new content:

- `Legacy/` — portable editor/model source derived from the original 2.1.1.0 compiler path.
- `Core/LegacyParamCompiler.cs` — semantic character/stage parameter compiler.
- `Core/LegacyUiCompiler.cs` — character/stage selection UI generation.
- `Assets/Payload/nsc_param_base.zip` — known-good baseline used for semantic output; this is intentionally large.
- `scripts/validate_phase2b.py` — CI preflight.

After applying, GitHub Actions should upload exactly one signed Phase 2B APK.

Do not reuse an old APK produced before the Phase 2B commit; the application version is 8 / display version 0.3.1.
