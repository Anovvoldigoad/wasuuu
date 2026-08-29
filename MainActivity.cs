using Android.App;
using Android.Content;
using Android.Database;
using AndroidUri = Android.Net.Uri;
using Android.OS;
using Android.Provider;
using Android.Views;
using Android.Widget;
using Android.Graphics;
using Android.Graphics.Drawables;
using NSC_ModManager_Android.Core;

namespace NSC_ModManager_Android;

[Activity(Label = "NSC Mod Manager Android", MainLauncher = true, Exported = true)]
public sealed class MainActivity : Activity
{
    const int PickModRequest = 1001;
    const int PickGameFolderRequest = 1002;
    AndroidPrefs _prefs = null!;
    EditText _gamePath = null!;
    EditText _modsPath = null!;
    ListView _list = null!;
    TextView _status = null!;
    TextView _runtimeFixBadge = null!;
    Button _compileButton = null!;
    readonly ModRepository _repo = new();
    readonly ModInstaller _installer = new();
    readonly AndroidCompiler _compiler = new();
    List<ModInfo> _mods = new();
    string _payloadZip = "";
    string _baseParamZip = "";
    string _messageBaseZip = "";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _prefs = new AndroidPrefs(this);
        BootstrapAssets();
        BuildUi();
        RefreshMods();
    }

    void BootstrapAssets()
    {
        string baseDir = FilesDir?.AbsolutePath ?? throw new InvalidOperationException("Android FilesDir is unavailable.");
        CopyBundledAsset("Resources/TemplateImages/stage_icon.dds",
            Path.Combine(baseDir, "Resources", "TemplateImages", "stage_icon.dds"));
        _payloadZip = Path.Combine(baseDir, "Payload", "moddingapi_payload.zip");
        CopyBundledAsset("Payload/moddingapi_payload.zip", _payloadZip);
        _baseParamZip = Path.Combine(baseDir, "Payload", "nsc_param_base.zip");
        CopyBundledAsset("Payload/nsc_param_base.zip", _baseParamZip);
        _messageBaseZip = Path.Combine(baseDir, "Payload", "nsc_message_base.zip");
        CopyBundledAsset("Payload/nsc_message_base.zip", _messageBaseZip);
        Directory.SetCurrentDirectory(baseDir);
    }

    void CopyBundledAsset(string assetName, string destination)
    {
        string? dir = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        string temp = destination + ".new";
        if (File.Exists(temp)) File.Delete(temp);
        using (var src = Assets?.Open(assetName) ?? throw new FileNotFoundException("APK asset missing: " + assetName))
        using (var dst = File.Create(temp))
            src.CopyTo(dst);
        File.Move(temp, destination, true);
    }

    void BuildUi()
    {
        Window?.SetStatusBarColor(Color.Rgb(7, 10, 16));
        Window?.SetNavigationBarColor(Color.Rgb(7, 10, 16));

        var shell = new FrameLayout(this);
        shell.SetBackgroundColor(Color.Rgb(7, 10, 16));
        shell.AddView(new StormBackdropView(this), new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

        var scroll = new ScrollView(this) { FillViewport = true };
        scroll.VerticalScrollBarEnabled = false;
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetPadding(Dp(16), Dp(18), Dp(16), Dp(36));
        scroll.AddView(root);
        shell.AddView(scroll, new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

        var header = Panel();
        var headerRow = Row();
        var logo = new ImageView(this);
        logo.SetImageResource(Resource.Drawable.ic_launcher_foreground);
        logo.SetPadding(Dp(2), Dp(2), Dp(2), Dp(2));
        headerRow.AddView(logo, new LinearLayout.LayoutParams(Dp(72), Dp(72)) { RightMargin = Dp(12) });
        var titleCol = new LinearLayout(this) { Orientation = Orientation.Vertical, Gravity = GravityFlags.CenterVertical };
        var title = new TextView(this) { Text = "NSC MOD MANAGER", TextSize = 23 };
        title.SetTextColor(Color.White);
        title.SetTypeface(Android.Graphics.Typeface.Default, Android.Graphics.TypefaceStyle.Bold);
        titleCol.AddView(title);
        var sub = new TextView(this) { Text = "ANDROID ARM64  •  STORM CONNECTIONS", TextSize = 11 };
        sub.SetTextColor(Color.Rgb(122, 174, 255));
        titleCol.AddView(sub);
        var version = new TextView(this) { Text = "v0.5.0  •  native compiler + UltimateStormAPI", TextSize = 11 };
        version.SetTextColor(Color.Rgb(166, 174, 192));
        titleCol.AddView(version);
        headerRow.AddView(titleCol, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
        header.AddView(headerRow);

        var badgeRow = Row();
        _runtimeFixBadge = Badge("SC 1.70 FIX  •  BUNDLED", Color.Rgb(79, 130, 220));
        badgeRow.AddView(_runtimeFixBadge, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1) { RightMargin = Dp(6) });
        var nativeBadge = Badge("ARM64 CPK  •  NATIVE", Color.Rgb(115, 87, 190));
        badgeRow.AddView(nativeBadge, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1) { LeftMargin = Dp(6) });
        header.AddView(badgeRow);
        AddPanel(root, header);

        var game = Panel();
        AddSectionTitle(game, "GAME SETUP");
        game.AddView(Description("Pilih folder root Storm Connections. Runtime compatibility fix SC 1.70 akan dipasang otomatis bersama ModdingAPI."));
        game.AddView(FieldLabel("Game directory"));
        _gamePath = StyledEditText(_prefs.GamePath, "/storage/.../Storm Connections");
        game.AddView(_gamePath);
        var gameRow = Row();
        gameRow.AddView(MakeButton("Select Folder", (_, _) => PickGameFolder(), true));
        gameRow.AddView(MakeButton("Save / Check", (_, _) => SaveGamePath()));
        game.AddView(gameRow);
        var storageRow = Row();
        storageRow.AddView(MakeButton("Storage Access", (_, _) => RequestAllFilesAccess()));
        game.AddView(storageRow);
        AddPanel(root, game);

        var mods = Panel();
        AddSectionTitle(mods, "MOD LIBRARY");
        mods.AddView(FieldLabel("Mod storage directory"));
        _modsPath = StyledEditText(_prefs.ModsPath, "/storage/emulated/0/NSC-ModManager/mods");
        mods.AddView(_modsPath);
        var modButtons = Row();
        modButtons.AddView(MakeButton("Install Mod", (_, _) => PickMod(), true));
        modButtons.AddView(MakeButton("Refresh", (_, _) => RefreshMods()));
        mods.AddView(modButtons);

        _list = new ListView(this) { ChoiceMode = ChoiceMode.Single };
        _list.SetBackgroundDrawable(RoundRect(Color.Argb(205, 13, 17, 26), Dp(12), Color.Argb(120, 86, 111, 157), Dp(1)));
        _list.Divider = new ColorDrawable(Color.Argb(70, 120, 150, 200));
        _list.DividerHeight = 1;
        _list.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(300))
        {
            TopMargin = Dp(8), BottomMargin = Dp(8)
        };
        mods.AddView(_list);
        var actions = Row();
        actions.AddView(MakeButton("Enable / Disable", (_, _) => ToggleSelected()));
        actions.AddView(MakeButton("Delete", (_, _) => DeleteSelected()));
        mods.AddView(actions);
        AddPanel(root, mods);

        var runtime = Panel();
        AddSectionTitle(runtime, "COMPILE & RUNTIME");
        runtime.AddView(Description("Compile menggabungkan resource/parameter mod, memasang UltimateStormAPI, lalu mengaktifkan SC 1.70 condition compatibility fix secara otomatis."));
        var compilerActions = Row();
        _compileButton = MakeButton("COMPILE MODS", async (_, _) => await CompileModsAsync(), true);
        compilerActions.AddView(_compileButton);
        compilerActions.AddView(MakeButton("Install / Update API", (_, _) => InstallModdingApiOnly()));
        runtime.AddView(compilerActions);
        var maintenance = Row();
        maintenance.AddView(MakeButton("Clear Compiled", (_, _) => ConfirmClearCompiledMods()));
        maintenance.AddView(MakeButton("Remove API", (_, _) => ConfirmRemoveModdingApi()));
        runtime.AddView(maintenance);
        AddPanel(root, runtime);

        var tools = Panel();
        AddSectionTitle(tools, "ADVANCED TOOLS");
        var apiProbe = Row();
        apiProbe.AddView(MakeButton("Arm API Probe", (_, _) => ArmApiProbe()));
        apiProbe.AddView(MakeButton("Check Runtime", (_, _) => CheckApiRuntime()));
        tools.AddView(apiProbe);
        var apiDiagnostics = Row();
        apiDiagnostics.AddView(MakeButton("Toggle API Debug", (_, _) => ToggleApiDebug()));
        apiDiagnostics.AddView(MakeButton("Export Diagnostics", (_, _) => ExportApiLog()));
        tools.AddView(apiDiagnostics);
        var cpk = Row();
        cpk.AddView(MakeButton("CPK Pack + Extract Self-Test", (_, _) => CpkSelfTest()));
        tools.AddView(cpk);
        AddPanel(root, tools);

        var statusPanel = Panel();
        AddSectionTitle(statusPanel, "STATUS");
        _status = new TextView(this) { Text = "Ready", TextSize = 13 };
        _status.SetTextColor(Color.Rgb(218, 228, 244));
        _status.SetTextIsSelectable(true);
        _status.SetPadding(Dp(12), Dp(12), Dp(12), Dp(12));
        _status.SetBackgroundDrawable(RoundRect(Color.Argb(210, 5, 8, 14), Dp(10), Color.Argb(100, 103, 142, 202), Dp(1)));
        statusPanel.AddView(_status);
        AddPanel(root, statusPanel);

        SetContentView(shell);
        UpdateRuntimeFixBadge();
    }

    int Dp(int value) => (int)(value * Resources!.DisplayMetrics!.Density + 0.5f);

    LinearLayout Row() => new(this)
    {
        Orientation = Orientation.Horizontal,
        Gravity = GravityFlags.CenterVertical
    };

    LinearLayout Panel()
    {
        var panel = new LinearLayout(this) { Orientation = Orientation.Vertical };
        panel.SetPadding(Dp(14), Dp(14), Dp(14), Dp(14));
        panel.SetBackgroundDrawable(RoundRect(Color.Argb(232, 16, 20, 30), Dp(16), Color.Argb(120, 91, 120, 170), Dp(1)));
        panel.Elevation = Dp(2);
        return panel;
    }

    void AddPanel(LinearLayout root, View panel)
    {
        root.AddView(panel, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        {
            BottomMargin = Dp(12)
        });
    }

    void AddSectionTitle(LinearLayout parent, string text)
    {
        var label = new TextView(this) { Text = text, TextSize = 12 };
        label.SetTextColor(Color.Rgb(118, 174, 255));
        label.SetTypeface(Android.Graphics.Typeface.Default, Android.Graphics.TypefaceStyle.Bold);
        label.SetPadding(0, 0, 0, Dp(8));
        parent.AddView(label);
    }

    TextView Description(string text)
    {
        var v = new TextView(this) { Text = text, TextSize = 12 };
        v.SetTextColor(Color.Rgb(171, 181, 199));
        v.SetPadding(0, 0, 0, Dp(10));
        return v;
    }

    TextView FieldLabel(string text)
    {
        var v = new TextView(this) { Text = text, TextSize = 12 };
        v.SetTextColor(Color.Rgb(211, 219, 234));
        v.SetPadding(0, Dp(6), 0, Dp(4));
        return v;
    }

    EditText StyledEditText(string text, string hint)
    {
        var edit = new EditText(this) { Text = text, Hint = hint, TextSize = 13 };
        edit.SetSingleLine(true);
        edit.SetTextColor(Color.White);
        edit.SetHintTextColor(Color.Rgb(105, 115, 133));
        edit.SetPadding(Dp(12), Dp(8), Dp(12), Dp(8));
        edit.SetBackgroundDrawable(RoundRect(Color.Argb(225, 7, 10, 17), Dp(10), Color.Argb(105, 93, 132, 190), Dp(1)));
        edit.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        {
            BottomMargin = Dp(6)
        };
        return edit;
    }

    Button MakeButton(string text, EventHandler click, bool primary = false)
    {
        var b = new Button(this) { Text = text, TextSize = 11 };
        b.SetAllCaps(false);
        b.Click += click;
        b.SetTextColor(Color.White);
        b.SetPadding(Dp(8), 0, Dp(8), 0);
        Color fill = primary ? Color.Rgb(45, 100, 186) : Color.Rgb(31, 39, 55);
        Color stroke = primary ? Color.Rgb(116, 176, 255) : Color.Rgb(79, 98, 130);
        b.SetBackgroundDrawable(RoundRect(fill, Dp(10), stroke, Dp(1)));
        b.LayoutParameters = new LinearLayout.LayoutParams(0, Dp(48), 1)
        {
            LeftMargin = Dp(3), RightMargin = Dp(3), TopMargin = Dp(4), BottomMargin = Dp(4)
        };
        return b;
    }

    TextView Badge(string text, Color fill)
    {
        var b = new TextView(this) { Text = text, TextSize = 10, Gravity = GravityFlags.Center };
        b.SetTextColor(Color.White);
        b.SetPadding(Dp(8), Dp(7), Dp(8), Dp(7));
        b.SetBackgroundDrawable(RoundRect(Color.Argb(150, fill.R, fill.G, fill.B), Dp(20), Color.Argb(210, fill.R, fill.G, fill.B), Dp(1)));
        return b;
    }

    GradientDrawable RoundRect(Color fill, int radius, Color stroke, int strokeWidth)
    {
        var d = new GradientDrawable();
        d.SetColor(fill);
        d.SetCornerRadius(radius);
        if (strokeWidth > 0) d.SetStroke(strokeWidth, stroke);
        return d;
    }

    void UpdateRuntimeFixBadge()
    {
        if (_runtimeFixBadge is null) return;
        string path = _gamePath?.Text?.Trim() ?? _prefs.GamePath;
        bool installed = !string.IsNullOrWhiteSpace(path) && Directory.Exists(path) && ModdingApiInstaller.IsConditionCompatFixInstalled(path);
        _runtimeFixBadge.Text = installed ? "SC 1.70 FIX  •  INSTALLED" : "SC 1.70 FIX  •  READY";
    }

    void SetStatus(string text)
    {
        _status.Text = $"{DateTime.Now:HH:mm:ss}  {text}";
        UpdateRuntimeFixBadge();
    }

    void RequestAllFilesAccess()
    {
        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R && !Android.OS.Environment.IsExternalStorageManager)
            {
                var uri = AndroidUri.Parse("package:" + PackageName);
                StartActivity(new Intent(Settings.ActionManageAppAllFilesAccessPermission, uri));
            }
            else SetStatus("Storage access already available (or not required on this Android version).");
        }
        catch
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
                StartActivity(new Intent(Settings.ActionManageAllFilesAccessPermission));
            else
                SetStatus("This Android version does not use All Files Access settings.");
        }
    }

    void SaveGamePath()
    {
        _prefs.GamePath = _gamePath.Text?.Trim() ?? "";
        _prefs.ModsPath = _modsPath.Text?.Trim() ?? "";
        var check = PathValidator.ValidateGamePath(_prefs.GamePath);
        SetStatus(check.Message);
    }

    void PickGameFolder()
    {
        try
        {
            var intent = new Intent(Intent.ActionOpenDocumentTree);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission
                            | ActivityFlags.GrantWriteUriPermission
                            | ActivityFlags.GrantPersistableUriPermission
                            | ActivityFlags.GrantPrefixUriPermission);
            StartActivityForResult(intent, PickGameFolderRequest);
        }
        catch (Exception ex)
        {
            SetStatus("Folder picker failed: " + ex.Message);
        }
    }

    void PickMod()
    {
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("application/octet-stream");
        intent.PutExtra(Intent.ExtraMimeTypes, new[] { "application/zip", "application/octet-stream", "application/x-zip-compressed" });
        StartActivityForResult(intent, PickModRequest);
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (resultCode != Result.Ok || data?.Data is null) return;

        if (requestCode == PickGameFolderRequest)
        {
            HandlePickedGameFolder(data);
            return;
        }
        if (requestCode != PickModRequest) return;

        try
        {
            _prefs.ModsPath = _modsPath.Text?.Trim() ?? _prefs.ModsPath;
            Directory.CreateDirectory(_prefs.ModsPath);
            string display = GetDisplayName(data.Data) ?? "mod.nsc";
            string ext = Path.GetExtension(display).ToLowerInvariant();
            if (ext is not (".nsc" or ".ensc" or ".uns" or ".unse" or ".nus4"))
                throw new InvalidDataException("Choose .nsc, .ensc, .uns, .unse, or .nus4 file.");
            string temp = Path.Combine(CacheDir?.AbsolutePath ?? FilesDir!.AbsolutePath, display);
            using (var src = ContentResolver?.OpenInputStream(data.Data) ?? throw new IOException("Cannot read selected mod file."))
            using (var dst = File.Create(temp)) src.CopyTo(dst);
            string installed = _installer.Install(temp, _prefs.ModsPath);
            SetStatus("Installed: " + installed);
            RefreshMods();
        }
        catch (Exception ex) { SetStatus("Install failed: " + ex.Message); }
    }

    void HandlePickedGameFolder(Intent data)
    {
        AndroidUri? uri = data.Data;
        if (uri is null) return;
        try
        {
            ActivityFlags takeFlags = data.Flags & (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
            try { ContentResolver?.TakePersistableUriPermission(uri, takeFlags); } catch { /* Direct-path access remains the compiler requirement. */ }

            if (!AndroidFolderPathResolver.TryResolve(uri, out string path, out string error))
            {
                SetStatus(error);
                return;
            }

            _gamePath.Text = path;
            _prefs.GamePath = path;
            var check = PathValidator.ValidateGamePath(path);
            string accessNote = string.IsNullOrWhiteSpace(error) ? check.Message : error;
            SetStatus("Selected: " + path + " | " + accessNote);
        }
        catch (Exception ex)
        {
            SetStatus("Cannot use selected game folder: " + ex.Message);
        }
    }

    string? GetDisplayName(AndroidUri uri)
    {
        using ICursor? cursor = ContentResolver?.Query(uri, null, null, null, null);
        if (cursor is not null && cursor.MoveToFirst())
        {
            int idx = cursor.GetColumnIndex(Android.Provider.IOpenableColumns.DisplayName);
            if (idx >= 0) return cursor.GetString(idx);
        }
        return uri.LastPathSegment;
    }

    void RefreshMods()
    {
        try
        {
            _prefs.ModsPath = _modsPath?.Text?.Trim() ?? _prefs.ModsPath;
            Directory.CreateDirectory(_prefs.ModsPath);
            _mods = _repo.Scan(_prefs.ModsPath).ToList();
            _list.Adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleListItemSingleChoice, _mods.Select(x => x.ToString()).ToArray());
            SetStatus($"{_mods.Count} mod(s) found; {_mods.Count(x => x.Enabled)} enabled.");
        }
        catch (Exception ex) { if (_status is not null) SetStatus("Refresh failed: " + ex.Message); }
    }

    ModInfo? Selected()
    {
        int p = _list.CheckedItemPosition;
        return p >= 0 && p < _mods.Count ? _mods[p] : null;
    }

    void ToggleSelected()
    {
        var mod = Selected();
        if (mod is null) { SetStatus("Select a mod first."); return; }
        try
        {
            _repo.SetEnabled(mod, !mod.Enabled);
            bool nowEnabled = mod.Enabled;
            string name = mod.Name;
            RefreshMods();
            SetStatus($"{name}: {(nowEnabled ? "enabled" : "disabled")}");
        }
        catch (Exception ex) { SetStatus(ex.Message); }
    }

    void DeleteSelected()
    {
        var mod = Selected();
        if (mod is null) { SetStatus("Select a mod first."); return; }
        new AlertDialog.Builder(this)
            .SetTitle("Delete mod?")
            .SetMessage(mod.Name)
            .SetNegativeButton("Cancel", (_, _) => { })
            .SetPositiveButton("Delete", (_, _) =>
            {
                try { _repo.Delete(mod); RefreshMods(); }
                catch (Exception ex) { SetStatus(ex.Message); }
            })
            .Show();
    }


    void InstallModdingApiOnly()
    {
        try
        {
            SaveGamePath();
            var check = PathValidator.ValidateGamePath(_prefs.GamePath);
            if (!check.Ok) { SetStatus(check.Message); return; }
            if (!File.Exists(_payloadZip)) { SetStatus("Bundled ModdingAPI payload is missing from APK."); return; }

            int count = ModdingApiInstaller.Install(_payloadZip, _prefs.GamePath);
            SetStatus($"ModdingAPI installed/updated: {count} file(s).");
        }
        catch (Exception ex)
        {
            SetStatus("ModdingAPI install failed: " + ex.Message);
        }
    }

    void ConfirmClearCompiledMods()
    {
        new AlertDialog.Builder(this)
            .SetTitle("Clear compiled mods?")
            .SetMessage("Removes NSC Mod Manager generated CPKs, restores backed-up game files, and resets ModdingAPI parameters. Installed mod packages remain in NSC-ModManager/mods and ModdingAPI stays installed.")
            .SetNegativeButton("Cancel", (_, _) => { })
            .SetPositiveButton("Clear", async (_, _) => await ClearCompiledModsAsync())
            .Show();
    }

    async Task ClearCompiledModsAsync()
    {
        try
        {
            SaveGamePath();
            var check = PathValidator.ValidateGamePath(_prefs.GamePath);
            if (!check.Ok) { SetStatus(check.Message); return; }
            SetStatus("Clearing generated mod files and restoring backups...");
            GameCleanupResult result = await Task.Run(() => GameCleanup.ClearCompiledMods(_payloadZip, _prefs.GamePath));
            SetStatus(result.ClearSummary);
        }
        catch (Exception ex)
        {
            SetStatus("Clear game failed: " + ex.Message);
        }
    }

    void ConfirmRemoveModdingApi()
    {
        new AlertDialog.Builder(this)
            .SetTitle("Remove ModdingAPI?")
            .SetMessage("Restores NSC Mod Manager backups, removes compiled outputs and files from the bundled ModdingAPI payload. Unrelated files are preserved. Installed mod packages in NSC-ModManager/mods are not deleted.")
            .SetNegativeButton("Cancel", (_, _) => { })
            .SetPositiveButton("Remove", async (_, _) => await RemoveModdingApiAsync())
            .Show();
    }

    async Task RemoveModdingApiAsync()
    {
        try
        {
            SaveGamePath();
            var check = PathValidator.ValidateGamePath(_prefs.GamePath);
            if (!check.Ok) { SetStatus(check.Message); return; }
            SetStatus("Removing ModdingAPI and restoring game backups...");
            GameCleanupResult result = await Task.Run(() => GameCleanup.RemoveModdingApi(_payloadZip, _prefs.GamePath));
            SetStatus(result.RemoveApiSummary);
        }
        catch (Exception ex)
        {
            SetStatus("Remove ModdingAPI failed: " + ex.Message);
        }
    }

    async Task CompileModsAsync()
    {
        if (!_compileButton.Enabled) return;
        try
        {
            SaveGamePath();
            var check = PathValidator.ValidateGamePath(_prefs.GamePath);
            if (!check.Ok) { SetStatus(check.Message); return; }
            if (!File.Exists(_payloadZip)) { SetStatus("Bundled ModdingAPI payload is missing from APK."); return; }
            if (!File.Exists(_baseParamZip)) { SetStatus("Bundled NSC parameter baseline is missing from APK."); return; }
            if (!File.Exists(_messageBaseZip)) { SetStatus("Bundled NSC localization baseline is missing from APK."); return; }

            _compileButton.Enabled = false;
            SetStatus("Starting compile...");
            string work = Path.Combine(CacheDir?.AbsolutePath ?? FilesDir!.AbsolutePath, "compile_work");
            IProgress<string> progress = new Progress<string>(message => SetStatus(message));

            CompileResult result = await Task.Run(() =>
                _compiler.Compile(_prefs.GamePath, _prefs.ModsPath, _payloadZip, _baseParamZip, _messageBaseZip, work, message => progress.Report(message)));

            string suffix = result.Warnings.Count == 0
                ? ""
                : $" | {result.Warnings.Count} warning(s); see compile report";
            SetStatus(result.Summary + suffix);
        }
        catch (DllNotFoundException)
        {
            SetStatus("libcpkbridge.so missing. Build/install the Signed APK from GitHub Actions.");
        }
        catch (Exception ex)
        {
            string? errorPath = TryWriteCompileError(ex);
            SetStatus("Compile failed: " + ex.Message + (errorPath is null ? "" : " | see nsc_android_last_error.txt"));
        }
        finally
        {
            _compileButton.Enabled = true;
        }
    }

    string? TryWriteCompileError(Exception ex)
    {
        try
        {
            string game = _prefs.GamePath;
            if (string.IsNullOrWhiteSpace(game) || !Directory.Exists(game)) return null;
            string dir = Path.Combine(game, "moddingapi", "mods", "base_game");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "nsc_android_last_error.txt");
            File.WriteAllText(path,
                "NSC Mod Manager Android — v0.5.0 last compile error" + System.Environment.NewLine +
                "Time: " + DateTime.Now.ToString("O") + System.Environment.NewLine +
                "App: 0.5.0" + System.Environment.NewLine +
                "Game: " + game + System.Environment.NewLine + System.Environment.NewLine +
                ex.ToString());
            return path;
        }
        catch
        {
            return null;
        }
    }



    void ArmApiProbe()
    {
        try
        {
            SaveGamePath();
            var check = PathValidator.ValidateGamePath(_prefs.GamePath);
            if (!check.Ok) { SetStatus(check.Message); return; }
            string probe = Path.Combine(_prefs.GamePath, "moddingapi", "mods", "base_game", "NSCApiRuntimeProbe.dll");
            if (!File.Exists(probe))
                ModdingApiInstaller.InstallRuntimeProbe(_payloadZip, _prefs.GamePath);
            UltimateStormApiDiagnostics.ArmProbe(_prefs.GamePath);
            SetStatus("API probe armed. Fully close the game, launch it again, reproduce the issue, exit, then tap Check API Runtime.");
        }
        catch (Exception ex) { SetStatus("API probe arm failed: " + ex.Message); }
    }

    void CheckApiRuntime()
    {
        try
        {
            SaveGamePath();
            var check = PathValidator.ValidateGamePath(_prefs.GamePath);
            if (!check.Ok) { SetStatus(check.Message); return; }
            ApiProbeStatus status = UltimateStormApiDiagnostics.GetProbeStatus(_prefs.GamePath);
            SetStatus("API runtime " + status.State + ": " + status.Message);
        }
        catch (Exception ex) { SetStatus("API runtime check failed: " + ex.Message); }
    }

    void ToggleApiDebug()
    {
        try
        {
            SaveGamePath();
            var check = PathValidator.ValidateGamePath(_prefs.GamePath);
            if (!check.Ok) { SetStatus(check.Message); return; }
            bool enabled = UltimateStormApiDiagnostics.ToggleDebug(_prefs.GamePath);
            SetStatus(enabled
                ? "UltimateStormAPI debug enabled and runtime probe armed. Fully close/relaunch the game, reproduce the issue, then tap Check API Runtime."
                : "UltimateStormAPI debug disabled.");
        }
        catch (Exception ex)
        {
            SetStatus("API debug toggle failed: " + ex.Message);
        }
    }

    void ExportApiLog()
    {
        try
        {
            SaveGamePath();
            var check = PathValidator.ValidateGamePath(_prefs.GamePath);
            if (!check.Ok) { SetStatus(check.Message); return; }
            string root;
            if (string.IsNullOrWhiteSpace(_prefs.ModsPath))
                root = Path.Combine(Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath ?? "/storage/emulated/0", "NSC-ModManager");
            else
            {
                string mods = _prefs.ModsPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                root = string.Equals(Path.GetFileName(mods), "mods", StringComparison.OrdinalIgnoreCase)
                    ? (Path.GetDirectoryName(mods) ?? mods)
                    : mods;
            }
            string export = Path.Combine(root, "logs");
            IReadOnlyList<string> files = UltimateStormApiDiagnostics.ExportDiagnostics(_prefs.GamePath, export);
            SetStatus($"Exported {files.Count} API diagnostic file(s) to {export}");
        }
        catch (Exception ex)
        {
            SetStatus("API log export failed: " + ex.Message);
        }
    }

    void CpkSelfTest()
    {
        try
        {
            string cache = CacheDir?.AbsolutePath ?? FilesDir!.AbsolutePath;
            string work = Path.Combine(cache, "cpk_test_input");
            string extract = Path.Combine(cache, "cpk_test_extract");
            if (Directory.Exists(work)) Directory.Delete(work, true);
            if (Directory.Exists(extract)) Directory.Delete(extract, true);
            Directory.CreateDirectory(work);
            File.WriteAllText(Path.Combine(work, "hello.txt"), "NSC Android CPK bridge pack/extract test");
            string cpk = Path.Combine(cache, "cpk_test.cpk");
            if (File.Exists(cpk)) File.Delete(cpk);

            int packCode = NativeCpk.Pack(work, cpk, false, 1);
            if (packCode != 0 || !File.Exists(cpk))
            {
                SetStatus($"CPK pack failed (exit {packCode}).");
                return;
            }

            Directory.CreateDirectory(extract);
            int extractCode = NativeCpk.Extract(cpk, extract);
            string restored = Path.Combine(extract, "hello.txt");
            bool ok = extractCode == 0 && File.Exists(restored) && File.ReadAllText(restored).Contains("pack/extract test", StringComparison.Ordinal);
            SetStatus(ok
                ? $"CPK native pack+extract OK: {new FileInfo(cpk).Length:N0} bytes"
                : $"CPK extract self-test failed (exit {extractCode}).");
        }
        catch (DllNotFoundException) { SetStatus("libcpkbridge.so missing. Build through included GitHub Action."); }
        catch (Exception ex) { SetStatus("CPK test failed: " + ex.Message); }
    }
}

internal sealed class StormBackdropView : View
{
    readonly Paint _particle = new(PaintFlags.AntiAlias);
    readonly Paint _line = new(PaintFlags.AntiAlias);
    readonly float[] _px = new float[34];
    readonly float[] _py = new float[34];
    readonly float[] _speed = new float[34];

    public StormBackdropView(Context context) : base(context)
    {
        SetLayerType(LayerType.Software, null);
        var random = new Random(170);
        for (int i = 0; i < _px.Length; i++)
        {
            _px[i] = (float)random.NextDouble();
            _py[i] = (float)random.NextDouble();
            _speed[i] = 0.018f + (float)random.NextDouble() * 0.045f;
        }
        _line.StrokeWidth = 1.2f;
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        if (Width <= 0 || Height <= 0) return;

        long now = SystemClock.ElapsedRealtime();
        float t = (now % 120000L) / 1000f;

        _particle.Color = Color.Argb(70, 75, 137, 235);
        for (int i = 0; i < _px.Length; i++)
        {
            float x = ((_px[i] + t * _speed[i]) % 1f) * Width;
            float y = ((_py[i] + t * _speed[(_px.Length - 1) - i] * 0.34f) % 1f) * Height;
            float r = 1.2f + (i % 4) * 0.55f;
            canvas.DrawCircle(x, y, r, _particle);
        }

        float cx = Width * 0.78f;
        float cy = Height * 0.18f;
        for (int i = 0; i < 4; i++)
        {
            int alpha = 28 - i * 5;
            _line.Color = Color.Argb(alpha, 137, 109, 255);
            _line.SetStyle(Paint.Style.Stroke);
            float radius = Width * (0.22f + i * 0.085f) + (float)Math.Sin(t * 0.55f + i) * 14f;
            canvas.DrawCircle(cx, cy, radius, _line);
        }

        _line.Color = Color.Argb(20, 90, 153, 255);
        _line.StrokeWidth = 2.2f;
        float offset = (t * 38f) % (Width + 260f) - 130f;
        canvas.DrawLine(offset, Height * 0.65f, offset + 260f, Height * 0.35f, _line);
        canvas.DrawLine(offset - 180f, Height * 0.82f, offset + 120f, Height * 0.48f, _line);

        PostInvalidateOnAnimation();
    }
}
