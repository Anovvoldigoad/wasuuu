# Apply Phase 2B over Phase 2A.1

Overwrite/add every file/folder from this patch at the repository root.
Do not delete `native/` or `Properties/` from Phase 2A.1; Phase 2B reuses them unchanged.

Then run the GitHub Actions workflow `Build Android APK`.
Expected artifact: `NSC-ModManager-Android-Phase2B-arm64-net10`.
Expected APK: `NSC-ModManager-Android-Phase2B-Signed.apk`.
