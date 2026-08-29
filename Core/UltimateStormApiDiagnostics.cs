namespace NSC_ModManager_Android.Core;

public static class UltimateStormApiDiagnostics
{
    public static bool ToggleDebug(string gamePath)
    {
        string config = Path.Combine(gamePath, "moddingapi", "config.ini");
        if (!File.Exists(config))
            throw new FileNotFoundException("ModdingAPI config.ini not found. Install/compile ModdingAPI first.", config);

        string[] lines = File.ReadAllLines(config);
        bool currentlyEnabled = lines.Any(x => IsEnabled(x, "enable_debug"))
                             || lines.Any(x => IsEnabled(x, "enable_console"));
        bool enable = !currentlyEnabled;

        SetKey(lines, "enable_debug", enable ? "1" : "0");
        SetKey(lines, "enable_console", enable ? "1" : "0");
        File.WriteAllLines(config, lines);
        return enable;
    }

    public static IReadOnlyList<string> ExportLogs(string gamePath, string exportRoot)
    {
        Directory.CreateDirectory(exportRoot);
        var candidates = new[]
        {
            Path.Combine(gamePath, "console.log"),
            Path.Combine(gamePath, "imgui_log.txt"),
            Path.Combine(gamePath, "moddingapi", "console.log"),
            Path.Combine(gamePath, "moddingapi", "imgui_log.txt"),
        };

        var copied = new List<string>();
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        foreach (string src in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(src)) continue;
            string name = $"{Path.GetFileNameWithoutExtension(src)}_{stamp}{Path.GetExtension(src)}";
            string dst = Path.Combine(exportRoot, name);
            File.Copy(src, dst, true);
            copied.Add(dst);
        }
        return copied;
    }

    private static bool IsEnabled(string line, string key)
    {
        string trimmed = line.Trim();
        if (!trimmed.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase)) return false;
        return trimmed[(trimmed.IndexOf('=') + 1)..].Trim() == "1";
    }

    private static void SetKey(string[] lines, string key, string value)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (trimmed.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = key + "=" + value;
                return;
            }
        }
        throw new InvalidDataException($"ModdingAPI config is missing '{key}'.");
    }
}
