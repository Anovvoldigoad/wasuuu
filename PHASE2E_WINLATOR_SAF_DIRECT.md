# Phase 2E — v0.5.3 Winlator SAF Direct

This phase adds a no-root Storage Access Framework backend for Winlator document-tree exports. It preserves the existing direct-storage and root backends.

Key source files:
- `Core/SafDocumentTree.cs` — generic DocumentsContract/ContentResolver tree operations.
- `Core/SafGameBridge.cs` — compile/install/cleanup bridge using a local compiler shadow and remote SAF commits.
- `Core/AndroidPrefs.cs` — persisted `GameAccessMode`, tree URI, and display label.
- `MainActivity.cs` — picker routing, persistable permission, write probe, and backend dispatch.

The implementation is intentionally feature-driven and contains no character/mod-specific paths or IDs.
