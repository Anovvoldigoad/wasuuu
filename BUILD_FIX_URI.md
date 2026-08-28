# Build Fix Phase 1.2 — Android Uri ambiguity

Fixed CS0104 caused by `Uri` resolving to both `Android.Net.Uri` and `System.Uri`.

Changes in `MainActivity.cs`:
- Added alias: `using AndroidUri = Android.Net.Uri;`
- `Uri.Parse(...)` -> `AndroidUri.Parse(...)`
- `GetDisplayName(Uri uri)` -> `GetDisplayName(AndroidUri uri)`

No other unqualified `Uri` references remain in the C# source.
