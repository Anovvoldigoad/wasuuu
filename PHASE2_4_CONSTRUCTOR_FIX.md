# Phase 2.4 - WinForms pre-handle startup fix

Observed Phase 2.3 startup log stops after `MainForm: constructor entered`, before `BuildUi begin`.

Changes:
- Keep MainForm constructor minimal before the HWND is visible.
- Remove custom Segoe UI font, CenterScreen and MinimumSize from pre-handle startup.
- Add checkpoints around Text and basic Size properties.
- Defer BuildUi and TitleViewModel construction until the Form `Shown` event.
- Bind the compatibility dispatcher only after the real MainForm HWND exists.
- Keep the native window alive if post-show initialization throws, and write the exception to `winlator_startup.log`.

Expected checkpoints:
- `MainForm: Text set`
- `MainForm: basic size set`
- `MainForm: constructor minimal setup complete`
- `MainForm: Shown fired; native HWND is alive`
- then detailed BuildUi checkpoints.
