# NSC Mod Manager Android — Phase 2C.1

Native Android ARM64 port of the NSC ModManager workflow. No Winlator/Wine is required.

Current verified foundation from earlier phases:
- Android app launches on-device.
- native ARM64 CPK pack/extract bridge works on-device.
- character/stage semantic merge can produce a playable mod character on-device.

Phase 2C adds generic per-mod capability discovery and portable localization merge for `messageInfo.bin.xfbin`. It intentionally treats development mods only as regression fixtures; compilation is dispatched from detected structures/features rather than hardcoded character names.

See `PHASE2C_GENERIC_COMPAT.md` for supported and pending feature families.

Build target: .NET 10 Android, `android-arm64`.


## Phase 2C.1 usability
- Select Game Folder with Android folder picker and direct filesystem path resolution.
- Clear Compiled Mods restores backups and keeps ModdingAPI installed.
- Remove ModdingAPI removes bundled payload/compiler output without recursively deleting unrelated user files.
