# v0.5.3 BuildFix1

This fixes the GitHub Actions C# build failure where `Core/RootGameBridge.cs` was present but `Core/RootShell.cs` was missing from the overlay-applied repository.

Apply to the repository root and replace files, then push/re-run Actions.

Files included:
- `Core/RootShell.cs`
- `scripts/validate_phase2c.py` (now fails early if RootGameBridge exists without RootShell)

No SAF, compiler, ModdingAPI, CPK, or gameplay logic is changed.
