# Phase 1.4 - Android native library packaging fix

GitHub Actions reached APK packaging successfully, but failed with XA4301 because
`libcpkbridge.so` existed twice inside the project tree:

- `native/build/libcpkbridge.so` (no ABI directory, invalid for Android packaging)
- `native-libs/arm64-v8a/libcpkbridge.so` (correct)

Phase 1.4:

1. Builds CMake output under `$RUNNER_TEMP/nsc-cpk-build`, outside the project tree.
2. Copies only the final ARM64 library to `native-libs/arm64-v8a/libcpkbridge.so`.
3. Removes the explicit `AndroidNativeLibrary` item from the csproj because the .NET
   Android SDK already auto-discovers the correctly placed `.so` file.
4. Cleans old `native/build` and `native-libs` folders before the native build.

This addresses the XA4301 duplicate/unknown-ABI packaging blocker from the Phase 1.3 log.
