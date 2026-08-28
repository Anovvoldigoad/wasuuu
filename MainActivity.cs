using Android.App;
using Android.Content;
using Android.Database;
using Android.Net;
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
    readonly ModRepository _repo = new();
    readonly ModInstaller _installer = new();
    List<ModInfo> _mods = new();

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
        try
        {
            string baseDir = FilesDir!.AbsolutePath;
            string dest = System.IO.Path.Combine(baseDir, "Resources", "TemplateImages", "stage_icon.dds");
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest)!);
            if (!File.Exists(dest))
            {
                using var src = Assets!.Open("Resources/TemplateImages/stage_icon.dds");
                using var dst = File.Create(dest);
                src.CopyTo(dst);
            }
            Directory.SetCurrentDirectory(baseDir);
        }
        catch { }
    }

    void BuildUi()
    {
        var scroll = new ScrollView(this);
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetPadding(24, 24, 24, 24);
        scroll.AddView(root);

        root.AddView(new TextView(this) { Text = "NSC Mod Manager — Android ARM64", TextSize = 22 });
        root.AddView(new TextView(this) { Text = "Native Android prototype. No Winlator/Wine required." });

        root.AddView(new TextView(this) { Text = "Game directory" });
        _gamePath = new EditText(this) { Text = _prefs.GamePath, Hint = "/storage/.../Storm Connections" };
        root.AddView(_gamePath);

        var gameRow = Row();
        gameRow.AddView(Button("Save / Check Path", (_, _) => SaveGamePath()));
        gameRow.AddView(Button("Storage Access", (_, _) => RequestAllFilesAccess()));
        root.AddView(gameRow);

        root.AddView(new TextView(this) { Text = "Mod storage directory" });
        _modsPath = new EditText(this) { Text = _prefs.ModsPath };
        root.AddView(_modsPath);

        var modButtons = Row();
        modButtons.AddView(Button("Install Mod", (_, _) => PickMod()));
        modButtons.AddView(Button("Refresh", (_, _) => RefreshMods()));
        root.AddView(modButtons);

        _list = new ListView(this) { ChoiceMode = ChoiceMode.Single };
        _list.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 520);
        root.AddView(_list);

        var actions = Row();
        actions.AddView(Button("Toggle", (_, _) => ToggleSelected()));
        actions.AddView(Button("Delete", (_, _) => DeleteSelected()));
        root.AddView(actions);

        var cpk = Row();
        cpk.AddView(Button("CPK Self-Test", (_, _) => CpkSelfTest()));
        root.AddView(cpk);

        _status = new TextView(this) { Text = "Ready", TextIsSelectable = true };
        root.AddView(_status);
        SetContentView(scroll);
    }

    LinearLayout Row() => new(this) { Orientation = Orientation.Horizontal };
    Button Button(string text, EventHandler click)
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
                var uri = Uri.Parse("package:" + PackageName);
                StartActivity(new Intent(Settings.ActionManageAppAllFilesAccessPermission, uri));
            }
            else SetStatus("Storage access already available (or not required on this Android version).");
        }
        catch { StartActivity(new Intent(Settings.ActionManageAllFilesAccessPermission)); }
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
            string ext = System.IO.Path.GetExtension(display).ToLowerInvariant();
            if (ext is not (".nsc" or ".ensc" or ".uns" or ".unse" or ".nus4"))
                throw new InvalidDataException("Choose .nsc, .ensc, .uns, .unse, or .nus4 file.");
            string temp = System.IO.Path.Combine(CacheDir!.AbsolutePath, display);
            using (var src = ContentResolver!.OpenInputStream(data.Data)!)
            using (var dst = File.Create(temp)) src.CopyTo(dst);
            string installed = _installer.Install(temp, _prefs.ModsPath);
            SetStatus("Installed: " + installed);
            RefreshMods();
        }
        catch (Exception ex) { SetStatus("Install failed: " + ex.Message); }
    }

    string? GetDisplayName(Uri uri)
    {
        using ICursor? cursor = ContentResolver?.Query(uri, null, null, null, null);
        if (cursor is not null && cursor.MoveToFirst())
        {
            int idx = cursor.GetColumnIndex(OpenableColumns.DisplayName);
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
            SetStatus($"{_mods.Count} mod(s) found.");
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
        var mod = Selected(); if (mod is null) { SetStatus("Select a mod first."); return; }
        try { _repo.SetEnabled(mod, !mod.Enabled); RefreshMods(); SetStatus($"{mod.Name}: {(mod.Enabled ? "enabled" : "disabled")}"); }
        catch (Exception ex) { SetStatus(ex.Message); }
    }

    void DeleteSelected()
    {
        var mod = Selected(); if (mod is null) { SetStatus("Select a mod first."); return; }
        new AlertDialog.Builder(this)
            .SetTitle("Delete mod?")
            .SetMessage(mod.Name)
            .SetNegativeButton("Cancel", (_, _) => { })
            .SetPositiveButton("Delete", (_, _) => { try { _repo.Delete(mod); RefreshMods(); } catch (Exception ex) { SetStatus(ex.Message); } })
            .Show();
    }

    void CpkSelfTest()
    {
        try
        {
            string work = System.IO.Path.Combine(CacheDir!.AbsolutePath, "cpk_test");
            if (Directory.Exists(work)) Directory.Delete(work, true);
            Directory.CreateDirectory(work);
            File.WriteAllText(System.IO.Path.Combine(work, "hello.txt"), "NSC Android CPK bridge test");
            string cpk = System.IO.Path.Combine(CacheDir.AbsolutePath, "cpk_test.cpk");
            if (File.Exists(cpk)) File.Delete(cpk);
            int code = NativeCpk.Pack(work, cpk, false, 1);
            SetStatus(code == 0 && File.Exists(cpk) ? $"CPK native OK: {new FileInfo(cpk).Length:N0} bytes" : $"CPK native failed (exit {code}).");
        }
        catch (DllNotFoundException) { SetStatus("libcpkbridge.so missing. Build through included GitHub Action."); }
        catch (Exception ex) { SetStatus("CPK test failed: " + ex.Message); }
    }
}
