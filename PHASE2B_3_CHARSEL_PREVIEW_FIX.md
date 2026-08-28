# Phase 2B.3 — Android character-select preview runtime fix

Phase 2B.2 built and launched on-device, but semantic compilation reached the legacy
`CharacterSelectParamModel.CharacterIconPath` setter. The original WPF model performs
UI preview I/O in that setter and attempts to load:

`Resources\Styles\UI\charsel_icons\pt_brank_emp.png`

That preview image is unrelated to XFBIN parsing/serialization and is not required by
the Android compiler. Phase 2B.3 preserves `CharacterIconPath` as model metadata while
removing all filesystem/BitmapFrame side effects from the setter.

No semantic character/stage merge rules were changed.
