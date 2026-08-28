# Apply Phase 2C over Phase 2B.3

Copy/overwrite every path from the Phase 2C patch ZIP into the repository root.

Important additions include:
- `Assets/Payload/nsc_message_base.zip`
- `Core/MessageInfoMerger.cs`
- `Core/ModCapabilityScanner.cs`
- updated semantic compiler/report/UI/workflow.

Build with the included GitHub Actions workflow. The expected artifact is:
`NSC-ModManager-Android-Phase2C-arm64-net10`

Expected APK:
`NSC-ModManager-Android-Phase2C-Signed.apk`
