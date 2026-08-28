using System;
using System.IO;
using System.Text.Json;

namespace NSC_ModManager.Properties
{
    /// <summary>
    /// Lightweight replacement for WPF/ApplicationSettingsBase settings.
    /// It intentionally stores settings beside the executable for Wine/Winlator portability.
    /// </summary>
    public sealed class Settings
    {
        private static readonly object Sync = new();
        private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "winlator_settings.json");
        private static Settings _default = Load();
        public static Settings Default => _default;

        public string StretchMode { get; set; } = "Uniform";
        public string BackgroundColor1 { get; set; } = "#9C000000";
        public string ButtonColor1 { get; set; } = "#9C000000";
        public string BackgroundImagePath { get; set; } = "UI/background/bg_toolbox_main.png";
        public string TextColor1 { get; set; } = "White";
        public string ModdingGroupLink { get; set; } = "https://discord.gg/naruto-storm-modding-server-841394026599022682";
        public string RootGameNSCFolder { get; set; } = string.Empty;
        public bool EnableMotionBlur { get; set; } = false;
        public bool MustUpgrade { get; set; } = false;
        public int StormVersion { get; set; } = 1;
        public string RootGameNS4Folder { get; set; } = string.Empty;
        public string ModManagerFolder { get; set; } = string.Empty;

        // Winlator edition additions.
        public bool LaunchAfterCompile { get; set; } = false;

        private static Settings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    return JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath), new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new Settings();
                }
            }
            catch { }
            return new Settings();
        }

        public void Save()
        {
            lock (Sync)
            {
                try
                {
                    File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
                }
                catch (Exception ex)
                {
                    NSC_ModManager.Compat.UiBridge.Log("Could not save settings: " + ex.Message);
                }
            }
        }

        public void Upgrade() { /* no-op: JSON settings have no version migration requirement */ }
        public void Reload() => _default = Load();
        public void Reset()
        {
            _default = new Settings();
            _default.Save();
        }
    }
}
