namespace NSC_ModManager_Android.Core;

public sealed class ModRepository
{
    public IReadOnlyList<ModInfo> Scan(string modsFolder)
    {
        if (string.IsNullOrWhiteSpace(modsFolder) || !Directory.Exists(modsFolder))
            return Array.Empty<ModInfo>();
        var result = new List<ModInfo>();
        foreach (string dir in Directory.EnumerateDirectories(modsFolder))
        {
            string iniPath = System.IO.Path.Combine(dir, "mod_config.ini");
            if (!File.Exists(iniPath)) continue;
            try
            {
                var ini = new IniFile(iniPath);
                string enabled = ini.Read("EnableMod", "ModManager", "true");
                result.Add(new ModInfo
                {
                    RootPath = dir,
                    Name = ini.Read("ModName", "ModManager", System.IO.Path.GetFileName(dir)),
                    Author = ini.Read("Author"),
                    Description = ini.Read("Description"),
                    Version = ini.Read("Version"),
                    Game = ini.Read("Game"),
                    LastUpdate = ini.Read("LastUpdate"),
                    Enabled = !enabled.Equals("false", StringComparison.OrdinalIgnoreCase)
                });
            }
            catch { }
        }
        return result.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public void SetEnabled(ModInfo mod, bool enabled)
    {
        new IniFile(mod.ConfigPath).Write("EnableMod", enabled ? "true" : "false");
        mod.Enabled = enabled;
    }

    public void Delete(ModInfo mod)
    {
        if (Directory.Exists(mod.RootPath)) Directory.Delete(mod.RootPath, true);
    }
}
