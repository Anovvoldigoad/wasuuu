namespace NSC_ModManager_Android.Core;

public sealed class ModRepository
{
    public IReadOnlyList<ModInfo> Scan(string modsFolder)
    {
        if (string.IsNullOrWhiteSpace(modsFolder) || !Directory.Exists(modsFolder))
            return Array.Empty<ModInfo>();

        var result = new List<ModInfo>();
        foreach (string iniPath in Directory.EnumerateFiles(modsFolder, "mod_config.ini", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            string? dir = Path.GetDirectoryName(iniPath);
            if (string.IsNullOrEmpty(dir)) continue;
            try
            {
                var ini = new IniFile(iniPath);
                string enabled = ini.Read("EnableMod", "ModManager", "true");
                result.Add(new ModInfo
                {
                    RootPath = dir,
                    Name = ini.Read("ModName", "ModManager", Path.GetFileName(dir)),
                    Author = ini.Read("Author"),
                    Description = ini.Read("Description"),
                    Version = ini.Read("Version"),
                    Game = ini.Read("Game"),
                    LastUpdate = ini.Read("LastUpdate"),
                    Enabled = !enabled.Equals("false", StringComparison.OrdinalIgnoreCase)
                });
            }
            catch
            {
                // A broken config should not make the whole mod list unavailable.
            }
        }

        return result
            .GroupBy(x => Path.GetFullPath(x.RootPath), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
