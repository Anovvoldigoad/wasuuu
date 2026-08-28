using Android.App;
using Android.Content;
using Android.Database;
using AndroidUri = Android.Net.Uri;
using Android.OS;
using Android.Provider;
using Android.Views;
using Android.Widget;
using NSC_ModManager_Android.Core;

namespace NSC_ModManager_Android;

[Activity(Label = "NSC Mod Manager Android", MainLauncher = true, Exported = true)]
public sealed class MainActivity : Activity
{
    const int PickModRequest = 1001;
    AndroidPrefs _prefs = null!;
    EditText _gamePath = null!;
    EditText _modsPath = null!;
    ListView _list = null!;
    TextView _status = null!;
    Button _compileButton = null!;
    readonly ModRepository _repo = new();
    readonly ModInstaller _installer = new();
    readonly AndroidCompiler _compiler = new();
    List<ModInfo> _mods = new();
    string _payloadZip = "";
    string _baseParamZip = "";

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
        var scroll = new ScrollView(this);
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetPadding(24, 24, 24, 24);
        scroll.AddView(root);

        root.AddView(new TextView(this) { Text = "NSC Mod Manager — Android ARM64", TextSize = 22 });
        root.AddView(new TextView(this) { Text = "Phase 2B: native CPK + semantic character/stage XFBIN compiler. No Winlator/Wine required." });

        root.AddView(new TextView(this) { Text = "Game directory" });
        _gamePath = new EditText(this) { Text = _prefs.GamePath, Hint = "/storage/.../Storm Connections" };
        root.AddView(_gamePath);

        var gameRow = Row();
        gameRow.AddView(MakeButton("Save / Check Path", (_, _) => SaveGamePath()));
        gameRow.AddView(MakeButton("Storage Access", (_, _) => RequestAllFilesAccess()));
        root.AddView(gameRow);

        root.AddView(new TextView(this) { Text = "Mod storage directory" });
        _modsPath = new EditText(this) { Text = _prefs.ModsPath };
        root.AddView(_modsPath);

        var modButtons = Row();
        modButtons.AddView(MakeButton("Install Mod", (_, _) => PickMod()));
        modButtons.AddView(MakeButton("Refresh", (_, _) => RefreshMods()));
        root.AddView(modButtons);

        _list = new ListView(this) { ChoiceMode = ChoiceMode.Single };
        _list.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 500);
        root.AddView(_list);

        var actions = Row();
        actions.AddView(MakeButton("Toggle", (_, _) => ToggleSelected()));
        actions.AddView(MakeButton("Delete", (_, _) => DeleteSelected()));
        root.AddView(actions);

        var compilerActions = Row();
        _compileButton = MakeButton("Compile Mods", async (_, _) => await CompileModsAsync());
        compilerActions.AddView(_compileButton);
        compilerActions.AddView(MakeButton("Install / Update ModdingAPI", (_, _) => InstallModdingApiOnly()));
        root.AddView(compilerActions);

        var cpk = Row();
        cpk.AddView(MakeButton("CPK Pack + Extract Self-Test", (_, _) => CpkSelfTest()));
        root.AddView(cpk);

        _status = new TextView(this) { Text = "Ready" };
        _status.SetTextIsSelectable(true);
        _status.SetPadding(0, 12, 0, 48);
        root.AddView(_status);
        SetContentView(scroll);
    }

    LinearLayout Row() => new(this) { Orientation = Orientation.Horizontal };

    Button MakeButton(string text, EventHandler click)
    {
        var b = new Button(this) { Text = text };
        b.Click += click;
        b.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        return b;
    }

    void SetStatus(string text) => _status.Text = $"{DateTime.Now:HH:mm:ss}  {text}";

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
        if (requestCode != PickModRequest || resultCode != Result.Ok || data?.Data is null) return;
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

            _compileButton.Enabled = false;
            SetStatus("Starting compile...");
            string work = Path.Combine(CacheDir?.AbsolutePath ?? FilesDir!.AbsolutePath, "compile_work");
            IProgress<string> progress = new Progress<string>(message => SetStatus(message));

            CompileResult result = await Task.Run(() =>
                _compiler.Compile(_prefs.GamePath, _prefs.ModsPath, _payloadZip, _baseParamZip, work, message => progress.Report(message)));

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
                "NSC Mod Manager Android — Phase 2B last compile error" + System.Environment.NewLine +
                "Time: " + DateTime.Now.ToString("O") + System.Environment.NewLine +
                "App: 0.3.4" + System.Environment.NewLine +
                "Game: " + game + System.Environment.NewLine + System.Environment.NewLine +
                ex.ToString());
            return path;
        }
        catch
        {
            return null;
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
