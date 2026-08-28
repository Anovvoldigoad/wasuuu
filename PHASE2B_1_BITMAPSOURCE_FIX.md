# Phase 2B.1 — BitmapSource build fix

This patch fixes the only two C# errors seen in the Phase 2B Release compile:
`BitmapSource` was still referenced by `CharacterSelectParamModel` from the original WPF editor.

The Android port now supplies portable `BitmapSource`, `BitmapFrame`, and `BitmapImage` stand-ins in `Legacy/PortableUiStubs.cs`. They preserve the legacy model shape without pulling WPF into the Android build. Semantic character/stage compilation does not render these images.

No semantic parameter merge logic was changed.
