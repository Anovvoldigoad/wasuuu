# Apply Phase 2C.3

Base: Phase 2C.2.

Overwrite the files from the Phase 2C.3 patch into the repository root. The GitHub Actions workflow now builds a tiny Windows x64 `NSCApiRuntimeProbe.dll` with MinGW, injects it into the bundled ModdingAPI payload, then builds the Android APK.

After installing the APK:
1. Tap **Arm API Probe**. If needed, the app installs only the probe DLL without overwriting compiled ModdingAPI parameters.
2. Fully close/relaunch the game in GameHub, reproduce the advanced-jutsu issue, then exit.
3. Tap **Check API Runtime**.
4. Tap **Export API Diagnostics** to create a report under `NSC-ModManager/logs`.
