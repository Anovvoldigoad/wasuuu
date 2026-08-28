# Android port audit

## Reused from NSC ModManager 2.1.1.0
- Mod package extension behavior
- `mod_config.ini` layout/keys
- ZIP extraction workflow
- `.nus4` embedded-ZIP conversion logic (ported from `TitleViewModel.ExtractNus4`)

## Replaced
- WPF/ModernWpf -> native Android widgets
- Windows file dialogs -> Android document picker
- Windows settings -> Android `SharedPreferences`
- `YACpkTool.exe`/`CpkMaker.dll` path -> ARM64 NDK `libcpkbridge.so`

## CPK implementation
The build workflow fetches `darkruss48/cpk-toolkit`, a standalone C++17 tool that supports CPK extraction/packing, CRILAYLA and CPK modes 0-3. `native/cpkbridge.cpp` compiles its CLI `main.cpp` with `main` renamed to `cpk_tool_main` and exposes C ABI pack/extract functions to C# through P/Invoke.

## Main unresolved dependency
Full NSC compiler uses many old ViewModel classes as binary serializers. They are algorithmically useful but contaminated by WPF dialogs/image/editor code. Phase 2 should extract only their Open/Save binary methods or compile them with a headless compatibility layer targeting `net8.0-android`, then connect `TitleViewModel.CompileModAsyncProcess` logic to an Android `CompilerService`.
