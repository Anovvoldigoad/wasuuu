# Phase 2B.2 build fix

Fixes the two root causes shown by the Phase 2B.1 GitHub Actions log:

1. `System.Environment` is explicitly qualified in `MainActivity.cs` so it does not conflict with `Android.OS.Environment`.
2. `Core/LegacyParamCompiler.cs` imports `NSC_Toolbox.ViewModel`, where the NS4 legacy view-model classes actually live.

No semantic parameter merge algorithm was changed in this patch.
Application display version: 0.3.3.
