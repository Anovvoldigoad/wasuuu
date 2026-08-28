# Apply Phase 2B.3 over Phase 2B.2

Overwrite these files in the repository:
- `Legacy/Model/CharacterSelectParamModel.cs`
- `NSC-ModManager.Android.csproj`
- `.github/workflows/build-android.yml`

This patch removes legacy WPF character-select preview PNG I/O from the Android compile path.
