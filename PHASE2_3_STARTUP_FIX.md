# Phase 2.3 - Winlator ARM64EC startup fix

The Phase 2.2 startup log stopped immediately after `SetCompatibleTextRenderingDefault`.
The next statement constructed a `WindowsFormsSynchronizationContext` inside the WPF compatibility dispatcher.
On the tested Winlator ARM64EC Wine build this hidden marshaling window was followed by DPI-hosting calls and the X11 connection being destroyed.

Changes:
- Disable `WindowsFormsSynchronizationContext.AutoInstall` at startup.
- `CompatDispatcher` no longer creates a `WindowsFormsSynchronizationContext`.
- Startup only records the UI thread ID.
- After `MainForm` is visible, dispatcher marshaling uses `Control.Invoke` on the real main form.
- Added granular MainForm/BuildUi startup checkpoints to `winlator_startup.log`.

Expected startup log now progresses past:
`Compat dispatcher recorded UI thread (no hidden sync window)`
then through MainForm construction.
