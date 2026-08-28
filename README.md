# NSC Mod Manager Android — Phase 1

Native Android/ARM64 port started from NSC ModManager 2.1.1.0 behavior. This build does **not** use Wine, Winlator, WPF, WinForms, Proton, Box64 or FEX.

## Implemented now
- Native .NET for Android UI (`net8.0-android34.0`)
- ARM64-only APK
- All-files-storage permission helper for sideload use
- Editable real game path
- Configurable mods folder
- Install `.nsc`, `.ensc`, `.uns`, `.unse` ZIP-based mods
- `.nus4` converter/extractor ported from the 2.1.1.0 source
- Scan `mod_config.ini`
- Enable/disable mod
- Delete mod
- Native ARM64 CPK pack/extract bridge
- CPK bridge uses cross-platform `darkruss48/cpk-toolkit` compiled by Android NDK

## Not implemented yet
The full 2.1.1.0 parameter/XFBIN merge compiler is **not wired into the APK yet**. Phase 2 is to move the existing compiler engine behind a portable service and replace every `YACpkTool.exe` call with the native CPK bridge.

This separation is intentional: the APK/UI + filesystem + native CPK path should be proven first before moving the 6k+ line parameter compiler.

## Build
Push this directory to a GitHub repository and run **Actions → Build Android APK**. The workflow installs the .NET Android workload, clones the cross-platform CPK toolkit, builds `libcpkbridge.so` for `arm64-v8a`, and builds the APK.

Download artifact `NSC-ModManager-Android-arm64` and sideload the APK.

## First run
1. Tap **Storage Access** and grant “All files access”.
2. Paste the real GameHub game path in **Game directory**.
3. Tap **Save / Check Path**.
4. Keep the default mod directory or change it.
5. Tap **Install Mod** and select a supported mod archive.
6. Run **CPK Self-Test**. If it reports `CPK native OK`, the Windows-only `YACpkTool/CpkMaker` dependency has been replaced successfully for Android.

## Android storage limitation
Even `MANAGE_EXTERNAL_STORAGE` cannot magically enter another app's private `/data/data/...` sandbox. If GameHub stores the game in a private sandbox, Phase 1 cannot modify it directly. A shared/exposed GameHub directory works; otherwise a Shizuku/root-backed filesystem provider will be needed in a later phase.
