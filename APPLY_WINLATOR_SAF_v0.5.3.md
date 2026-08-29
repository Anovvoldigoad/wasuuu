# Apply v0.5.3 Winlator SAF Direct

Apply the overlay to a repository already based on v0.5.2 Root Winlator C. Replace existing files and add the two new Core SAF files.

Then push to GitHub and let the included workflow build the Android ARM64 signed APK.

Expected artifact:
`NSC-ModManager-Android-v0.5.3-Signed.apk`

First device test:
1. Tap `Select Folder / Winlator`.
2. Pick the Winlator provider shown by Android's system picker.
3. Navigate to the Storm Connections folder containing `NSUNSC.exe`.
4. Grant the folder permission.
5. Expected status: `Selected Winlator/SAF folder directly — no root required.`
6. Tap `Save / Check`.
7. Test `Install / Update API` before running a full compile.
8. Expected badge: `SC 1.70 FIX • SAF INSTALLED`.
9. Then test `Compile Mods`.

If the provider cannot create/write/delete files, selection fails at the temporary write probe instead of failing halfway through compilation.
