# Phase 2C.3 — Deterministic UltimateStormAPI Runtime Probe

Phase 2C.2 could verify every special API parameter after compile, but Android could
only *search* for UltimateStormAPI console logs. Under GameHub/Wine those logs may
be written to another working directory, so "log not found" did not prove whether
the API was actually loaded.

Phase 2C.3 builds a tiny Windows x64 plugin during GitHub Actions and injects it into
`moddingapi_payload.zip` as:

`moddingapi/mods/base_game/NSCApiRuntimeProbe.dll`

The plugin contains no gameplay hooks. It only writes markers next to the running
game executable:

- `nsc_api_probe_dllmain.txt` — Windows/Wine loaded the plugin DLL.
- `nsc_api_probe_initialized.txt` — UltimateStormAPI called `InitializePlugin()`.

The Android UI adds **Arm API Probe** and **Check API Runtime**. Arming deletes old
markers, preventing stale positives. **Export API Diagnostics** always writes a
report even if UltimateStormAPI produced no `console.log`; the report includes the
probe state, file presence/sizes/SHA-256, debug config and available markers/logs.

This is diagnostic infrastructure, not a Tobi-specific fix. It works for any mod
that requires the UltimateStormAPI runtime.

`Arm API Probe` can install only the probe entry from the bundled payload. It does not reinstall the full ModdingAPI payload, so already-merged special API parameters are not reset during diagnostics.
