# Android Build Fix — .NET 10

This revision updates the Android project from the EOL `net8.0-android34.0` workload to supported `.NET 10 for Android`.

Changes:
- TargetFramework: `net10.0-android`
- GitHub Actions SDK: `10.0.400`
- Added `global.json` pinned to SDK 10.0.400
- Replaced obsolete `AndroidSupportedAbis` with `RuntimeIdentifier=android-arm64`
- Updated InstallAndroidDependencies/build commands to .NET 10
- Artifact name now includes `net10`

Minimum Android version remains API 26 (Android 8.0).
