using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using NSC_ModManager.Compat;
using NSC_ModManager.Model;
using NSC_ModManager.Properties;
using NSC_ModManager.ViewModel;
using WpfVisibility = System.Windows.Visibility;

namespace NSC_ModManager.UI;

public sealed class MainForm : Form
{
    private TitleViewModel _vm = null!;
    private readonly ComboBox _game = new();
    private readonly TextBox _gameFolder = new();
    private readonly TextBox _modFolder = new();
    private readonly ListView _mods = new();
    private readonly CheckBox _enabled = new();
    private readonly CheckBox _launchAfterCompile = new();
    private readonly TextBox _log = new();
    private readonly Label _status = new();
    private readonly Button _compile = new();
    private readonly Button _install = new();
    private readonly Button _delete = new();
    private readonly Button _clean = new();
    private readonly Button _api = new();
    private readonly System.Windows.Forms.Timer _stateTimer = new() { Interval = 250 };
    private bool _updatingSelection;
    private bool _initializing = true;

    public MainForm()
    {
        WinlatorEntry.Trace("MainForm: constructor entered");

        // Keep the pre-handle constructor deliberately tiny for Wine/ARM64EC.
        // Complex controls and the original ViewModel are created only after
        // the native form handle has been shown successfully.
        WinlatorEntry.Trace("MainForm: setting Text");
        Text = "NSC Mod Manager 2.1.1.0 - Winlator Edition";
        WinlatorEntry.Trace("MainForm: Text set");

        WinlatorEntry.Trace("MainForm: setting basic size");
        Width = 900;
        Height = 650;
        WinlatorEntry.Trace("MainForm: basic size set");

        // Do not set a custom Font, CenterScreen, MinimumSize, or DPI autoscaling
        // before the first HWND exists. These paths are problematic in some
        // ARM64EC Wine builds.
        AutoScaleMode = AutoScaleMode.None;
        WinlatorEntry.Trace("MainForm: constructor minimal setup complete");

        Shown += MainForm_Shown;
        FormClosed += (_, _) =>
        {
            try
            {
                if (!_initializing)
                    SaveUiSettings();
            }
            catch (Exception ex)
            {
                WinlatorEntry.Trace("MainForm: FormClosed save failed: " + ex);
            }
            UiBridge.Message -= AppendLog;
        };
    }

    private void MainForm_Shown(object? sender, EventArgs e)
    {
        // Run once only. Unsubscribe immediately so accidental re-show does not
        // reconstruct the original NSC backend.
        Shown -= MainForm_Shown;
        WinlatorEntry.Trace("MainForm: Shown fired; native HWND is alive");

        try
        {
            System.Windows.Application.Current.Dispatcher.BindToControl(this);
            WinlatorEntry.Trace("MainForm: dispatcher bound to visible MainForm handle");

            WinlatorEntry.Trace("MainForm: BuildUi begin (post-show)");
            BuildUi();
            WinlatorEntry.Trace("MainForm: BuildUi complete (post-show)");

            UiBridge.Message += AppendLog;
            WinlatorEntry.Trace("MainForm: constructing TitleViewModel (post-show)");
            _vm = new TitleViewModel();
            WinlatorEntry.Trace("MainForm: TitleViewModel constructed");

            _game.Items.AddRange(new object[] { "Storm Connections", "Storm 4" });
            _game.SelectedIndex = Settings.Default.StormVersion == 2 ? 1 : 0;
            _launchAfterCompile.Checked = Settings.Default.LaunchAfterCompile;

            LoadSettingsIntoUi();
            _initializing = false;
            RefreshModListUi();
            UpdateBusyState();

            _stateTimer.Tick += (_, _) => UpdateBusyState();
            _stateTimer.Start();
            WinlatorEntry.Trace("MainForm: post-show initialization complete");
        }
        catch (Exception ex)
        {
            WinlatorEntry.Trace("MainForm: post-show initialization FAILED: " + ex);
            // Keep the empty native window alive so Wine remains debuggable.
            try
            {
                Text = "NSC Mod Manager - startup failed (see winlator_startup.log)";
            }
            catch { }
        }
    }

    private void BuildUi()
    {
        WinlatorEntry.Trace("MainForm.BuildUi: root layout");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        WinlatorEntry.Trace("MainForm.BuildUi: paths controls");
        var paths = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Padding = new Padding(0, 0, 0, 8) };
        paths.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        paths.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        paths.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        paths.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        paths.Controls.Add(new Label { Text = "Game", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 8, 0) }, 0, 0);
        _game.DropDownStyle = ComboBoxStyle.DropDownList;
        _game.Width = 190;
        paths.Controls.Add(_game, 1, 0);
        paths.SetColumnSpan(_game, 3);

        paths.Controls.Add(new Label { Text = "Game folder", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 8, 0) }, 0, 1);
        _gameFolder.Dock = DockStyle.Fill;
        paths.Controls.Add(_gameFolder, 1, 1);
        var browseGame = new Button { Text = "Browse...", AutoSize = true };
        browseGame.Click += (_, _) => BrowseGameFolder();
        paths.Controls.Add(browseGame, 2, 1);
        var launch = new Button { Text = "Launch EXE", AutoSize = true };
        launch.Click += (_, _) => LaunchGameDirect();
        paths.Controls.Add(launch, 3, 1);

        paths.Controls.Add(new Label { Text = "Mod folder", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 8, 0) }, 0, 2);
        _modFolder.Dock = DockStyle.Fill;
        paths.Controls.Add(_modFolder, 1, 2);
        var browseMods = new Button { Text = "Browse...", AutoSize = true };
        browseMods.Click += (_, _) => BrowseModFolder();
        paths.Controls.Add(browseMods, 2, 2);
        var refresh = new Button { Text = "Refresh", AutoSize = true };
        refresh.Click += (_, _) => RefreshAll();
        paths.Controls.Add(refresh, 3, 2);
        root.Controls.Add(paths, 0, 0);

        WinlatorEntry.Trace("MainForm.BuildUi: action controls");
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(0, 0, 0, 8) };
        _install.Text = "Install Mod";
        _install.AutoSize = true;
        _install.Click += (_, _) => InstallMod();
        _delete.Text = "Delete Mod";
        _delete.AutoSize = true;
        _delete.Click += (_, _) => DeleteSelectedMod();
        _compile.Text = "Compile Mods";
        _compile.AutoSize = true;
        _compile.Click += (_, _) => CompileMods();
        _clean.Text = "Clean Game";
        _clean.AutoSize = true;
        _clean.Click += (_, _) => CleanGame();
        _api.Text = "Install Modding API";
        _api.AutoSize = true;
        _api.Click += (_, _) => InstallApi();
        _enabled.Text = "Enabled";
        _enabled.AutoSize = true;
        _enabled.Padding = new Padding(8, 5, 8, 0);
        _enabled.CheckedChanged += (_, _) => ChangeEnabledState();
        _launchAfterCompile.Text = "Launch after compile";
        _launchAfterCompile.AutoSize = true;
        _launchAfterCompile.Padding = new Padding(8, 5, 0, 0);
        _launchAfterCompile.CheckedChanged += (_, _) =>
        {
            Settings.Default.LaunchAfterCompile = _launchAfterCompile.Checked;
            Settings.Default.Save();
        };
        actions.Controls.AddRange(new Control[] { _install, _delete, _compile, _clean, _api, _enabled, _launchAfterCompile });
        root.Controls.Add(actions, 0, 1);

        WinlatorEntry.Trace("MainForm.BuildUi: mod list");
        _mods.Dock = DockStyle.Fill;
        _mods.View = System.Windows.Forms.View.Details;
        _mods.FullRowSelect = true;
        _mods.HideSelection = false;
        _mods.MultiSelect = false;
        _mods.Columns.Add("On", 48);
        _mods.Columns.Add("Name", 270);
        _mods.Columns.Add("Author", 160);
        _mods.Columns.Add("Version", 90);
        _mods.Columns.Add("Game", 130);
        _mods.Columns.Add("Updated", 130);
        _mods.SelectedIndexChanged += (_, _) => SelectCurrentMod();
        _mods.DoubleClick += (_, _) =>
        {
            if (_mods.SelectedItems.Count == 1)
                ChangeEnabledState(toggleFirst: true);
        };
        root.Controls.Add(_mods, 0, 2);

        WinlatorEntry.Trace("MainForm.BuildUi: log/status controls");
        var logBox = new GroupBox { Text = "Status / log", Dock = DockStyle.Fill, Padding = new Padding(8) };
        _log.Dock = DockStyle.Fill;
        _log.Multiline = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.ReadOnly = true;
        _log.WordWrap = true;
        logBox.Controls.Add(_log);
        root.Controls.Add(logBox, 0, 3);

        var statusPanel = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 2 };
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _status.Text = "Ready";
        _status.AutoEllipsis = true;
        _status.Dock = DockStyle.Fill;
        _status.Padding = new Padding(0, 6, 0, 0);
        statusPanel.Controls.Add(_status, 0, 0);
        var openLog = new Button { Text = "Open error.log", AutoSize = true };
        openLog.Click += (_, _) => OpenErrorLog();
        statusPanel.Controls.Add(openLog, 1, 0);
        root.Controls.Add(statusPanel, 0, 4);

        _game.SelectedIndexChanged += (_, _) => ChangeGame();
        WinlatorEntry.Trace("MainForm.BuildUi: complete");
    }

    private bool IsStorm4 => _game.SelectedIndex == 1;

    private void ChangeGame()
    {
        if (_game.SelectedIndex < 0 || _initializing) return;
        Settings.Default.StormVersion = IsStorm4 ? 2 : 1;
        Settings.Default.Save();
        _vm.GameVersion = Settings.Default.StormVersion;
        _gameFolder.Text = IsStorm4 ? Settings.Default.RootGameNS4Folder : Settings.Default.RootGameNSCFolder;
        RefreshAll();
    }

    private void LoadSettingsIntoUi()
    {
        _vm.GameVersion = Settings.Default.StormVersion;
        _gameFolder.Text = IsStorm4 ? Settings.Default.RootGameNS4Folder : Settings.Default.RootGameNSCFolder;
        _modFolder.Text = Settings.Default.ModManagerFolder;
        AppendLog("Winlator Edition started. WPF/ModernWpf UI has been replaced with WinForms.");
        AppendLog("Compile uses the original 2.1.1.0 backend. Direct launch is disabled by default for GameHub users.");
    }

    private void SaveUiSettings()
    {
        Settings.Default.StormVersion = IsStorm4 ? 2 : 1;
        if (IsStorm4) Settings.Default.RootGameNS4Folder = _gameFolder.Text.Trim();
        else Settings.Default.RootGameNSCFolder = _gameFolder.Text.Trim();
        Settings.Default.ModManagerFolder = _modFolder.Text.Trim();
        Settings.Default.LaunchAfterCompile = _launchAfterCompile.Checked;
        Settings.Default.Save();
    }

    private void BrowseGameFolder()
    {
        using var dlg = new FolderBrowserDialog { Description = IsStorm4 ? "Select Storm 4 root folder" : "Select Storm Connections root folder", ShowNewFolderButton = false };
        if (Directory.Exists(_gameFolder.Text)) dlg.SelectedPath = _gameFolder.Text;
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        string expectedExe = IsStorm4 ? "NSUNS4.exe" : "NSUNSC.exe";
        if (!File.Exists(Path.Combine(dlg.SelectedPath, expectedExe)))
        {
            MessageBox.Show(this, $"{expectedExe} was not found in that folder.", "Wrong game folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _gameFolder.Text = dlg.SelectedPath;
        SaveUiSettings();
        SyncVmPaths();
    }

    private void BrowseModFolder()
    {
        using var dlg = new FolderBrowserDialog { Description = "Select folder where installed mods are stored", ShowNewFolderButton = true };
        if (Directory.Exists(_modFolder.Text)) dlg.SelectedPath = _modFolder.Text;
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _modFolder.Text = dlg.SelectedPath;
        SaveUiSettings();
        SyncVmPaths();
        RefreshAll();
    }

    private void SyncVmPaths()
    {
        _vm.GameVersion = IsStorm4 ? 2 : 1;
        _vm.RootFolderPath_field = Settings.Default.RootGameNSCFolder;
        _vm.RootFolderPathNS4_field = Settings.Default.RootGameNS4Folder;
        _vm.ModManagerFolder_field = Settings.Default.ModManagerFolder;
    }

    private void RefreshAll()
    {
        try
        {
            SaveUiSettings();
            SyncVmPaths();
            _vm.RefreshModList();
            RefreshModListUi();
            SetStatus($"Loaded {_vm.ModManagerList.Count} mod(s).", false);
        }
        catch (Exception ex) { ShowOperationError("Refresh failed", ex); }
    }

    private void RefreshModListUi()
    {
        string? selectedFolder = _vm.SelectedMod?.ModFolder;
        _mods.BeginUpdate();
        try
        {
            _mods.Items.Clear();
            foreach (var mod in _vm.ModManagerList)
            {
                var item = new ListViewItem(mod.EnableMod ? "Yes" : "No") { Tag = mod };
                item.SubItems.Add(mod.ModName ?? string.Empty);
                item.SubItems.Add(mod.Author ?? string.Empty);
                item.SubItems.Add(mod.Version ?? string.Empty);
                item.SubItems.Add(mod.Game ?? string.Empty);
                item.SubItems.Add(mod.LastUpdate ?? string.Empty);
                _mods.Items.Add(item);
                if (!string.IsNullOrEmpty(selectedFolder) && string.Equals(selectedFolder, mod.ModFolder, StringComparison.OrdinalIgnoreCase))
                    item.Selected = true;
            }
        }
        finally { _mods.EndUpdate(); }
    }

    private void SelectCurrentMod()
    {
        _updatingSelection = true;
        try
        {
            if (_mods.SelectedItems.Count != 1)
            {
                _vm.SelectedMod = null!;
                _enabled.Checked = false;
                return;
            }
            var mod = (ModManagerModel)_mods.SelectedItems[0].Tag!;
            _vm.SelectedMod = mod;
            _enabled.Checked = mod.EnableMod;
            SetStatus($"Selected: {mod.ModName}", false);
        }
        finally { _updatingSelection = false; }
    }

    private void ChangeEnabledState(bool toggleFirst = false)
    {
        if (_updatingSelection || _vm.SelectedMod is null) return;
        try
        {
            if (toggleFirst)
            {
                _updatingSelection = true;
                _enabled.Checked = !_enabled.Checked;
                _updatingSelection = false;
            }
            _vm.SelectedMod.EnableMod = _enabled.Checked;
            _vm.EnableModIsChecked();
            RefreshModListUi();
        }
        catch (Exception ex) { ShowOperationError("Could not update mod state", ex); }
    }

    private void InstallMod()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "Naruto Storm Mod|*.uns;*.unse;*.nsc;*.ensc;*.nus4|All files|*.*",
            CheckFileExists = true,
            Multiselect = false,
            Title = "Install mod"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            SaveUiSettings();
            SyncVmPaths();
            _vm.InstallMod(dlg.FileName);
            RefreshModListUi();
            SetStatus("Mod installed: " + Path.GetFileName(dlg.FileName));
        }
        catch (Exception ex) { ShowOperationError("Install failed", ex); }
    }

    private void DeleteSelectedMod()
    {
        if (_vm.SelectedMod is null) return;
        if (MessageBox.Show(this, $"Delete '{_vm.SelectedMod.ModName}' from the mod manager?", "Delete mod", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            _vm.DeleteMod();
            RefreshModListUi();
            SetStatus("Mod deleted.");
        }
        catch (Exception ex) { ShowOperationError("Delete failed", ex); }
    }

    private void CompileMods()
    {
        try
        {
            SaveUiSettings();
            SyncVmPaths();
            AppendLog(IsStorm4 ? "Starting Storm 4 compilation..." : "Starting Storm Connections compilation...");
            if (IsStorm4) _vm.CompileModsNS4();
            else _vm.CompileMods();
            UpdateBusyState();
        }
        catch (Exception ex) { ShowOperationError("Compile failed to start", ex); }
    }

    private void CleanGame()
    {
        try
        {
            SaveUiSettings();
            SyncVmPaths();
            if (IsStorm4) _vm.CleanGameAssetsNS4(true, false);
            else _vm.CleanGameAssets(true, false);
        }
        catch (Exception ex) { ShowOperationError("Clean failed", ex); }
    }

    private void InstallApi()
    {
        try
        {
            SaveUiSettings();
            SyncVmPaths();
            string root = IsStorm4 ? Settings.Default.RootGameNS4Folder : Settings.Default.RootGameNSCFolder;
            if (IsStorm4)
            {
                MessageBox.Show(this, "The original 2.1.1.0 InstallModdingAPI routine targets Storm Connections. Storm 4 API handling is left to the original compile path.", "Storm 4", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _vm.InstallModdingAPI(true, root);
        }
        catch (Exception ex) { ShowOperationError("API install failed", ex); }
    }

    private void LaunchGameDirect()
    {
        SaveUiSettings();
        string root = IsStorm4 ? Settings.Default.RootGameNS4Folder : Settings.Default.RootGameNSCFolder;
        string exe = Path.Combine(root, IsStorm4 ? "NSUNS4.exe" : "NSUNSC.exe");
        if (!File.Exists(exe))
        {
            MessageBox.Show(this, "Game executable was not found. If you use GameHub, just compile here and launch the game from GameHub.", "Launch", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo { FileName = exe, WorkingDirectory = root, UseShellExecute = true });
        }
        catch (Exception ex) { ShowOperationError("Game launch failed", ex); }
    }

    private void UpdateBusyState()
    {
        bool busy = _vm.LoadingStatePlay == WpfVisibility.Visible;
        _compile.Enabled = !busy;
        _install.Enabled = !busy;
        _delete.Enabled = !busy;
        _clean.Enabled = !busy;
        _api.Enabled = !busy;
        if (busy && string.IsNullOrWhiteSpace(_status.Text)) _status.Text = "Working...";
        if (!busy && _status.Text == "Working...") _status.Text = "Ready";
    }

    private void OpenErrorLog()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
        if (!File.Exists(path))
        {
            MessageBox.Show(this, "No error.log has been created yet.", "Log", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try { Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); }
        catch { AppendLog(File.ReadAllText(path)); }
    }

    private void ShowOperationError(string title, Exception ex)
    {
        AppendLog(title + ": " + ex);
        MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void AppendLog(string text)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action<string>(AppendLog), text); } catch { }
            return;
        }
        string line = $"[{DateTime.Now:HH:mm:ss}] {text}";
        _log.AppendText(line + Environment.NewLine);
        _status.Text = text;
    }

    private void SetStatus(string text) => SetStatus(text, true);

    private void SetStatus(string text, bool log)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action<string, bool>(SetStatus), text, log); } catch { }
            return;
        }
        _status.Text = text;
        if (log) _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
    }
}
