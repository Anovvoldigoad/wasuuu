using System.Security.Cryptography;
using System.Text;

namespace NSC_ModManager_Android.Core;

public sealed record ApiProbeStatus(string State, string Message, bool FreshDllMain, bool FreshInitialize);

public static class UltimateStormApiDiagnostics
{
    const string ArmMarker = "nsc_api_probe_armed.txt";
    const string DllMainMarker = "nsc_api_probe_dllmain.txt";
    const string InitMarker = "nsc_api_probe_initialized.txt";

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
        if (enable) ArmProbe(gamePath);
        return enable;
    }

    public static string ArmProbe(string gamePath)
    {
        Directory.CreateDirectory(gamePath);
        foreach (string name in new[] { DllMainMarker, InitMarker })
        {
            string path = Path.Combine(gamePath, name);
            if (File.Exists(path)) File.Delete(path);
        }

        string arm = Path.Combine(gamePath, ArmMarker);
        File.WriteAllText(arm,
            "NSC Mod Manager Android runtime probe armed" + System.Environment.NewLine +
            "Time: " + DateTime.Now.ToString("O") + System.Environment.NewLine);
        return arm;
    }

    public static ApiProbeStatus GetProbeStatus(string gamePath)
    {
        string probeDll = Path.Combine(gamePath, "moddingapi", "mods", "base_game", "NSCApiRuntimeProbe.dll");
        if (!File.Exists(probeDll))
            return new("PROBE_NOT_INSTALLED", "Runtime probe DLL is not installed. Run Install / Update ModdingAPI from v0.5.0, then arm the probe.", false, false);

        string arm = Path.Combine(gamePath, ArmMarker);
        DateTime armUtc = File.Exists(arm) ? File.GetLastWriteTimeUtc(arm) : DateTime.MinValue;
        bool dll = IsFresh(Path.Combine(gamePath, DllMainMarker), armUtc);
        bool init = IsFresh(Path.Combine(gamePath, InitMarker), armUtc);

        if (init)
            return new("LOADED", "UltimateStormAPI runtime + plugin loader detected: probe DLL loaded and InitializePlugin() was called.", dll, true);
        if (dll)
            return new("DLL_LOADED_INIT_MISSING", "Probe DLL was loaded, but InitializePlugin() was not called. UltimateStormAPI plugin initialization is incomplete.", true, false);

        string suffix = File.Exists(arm)
            ? "The probe was armed, but the game never loaded the plugin. This points to the UltimateStormAPI proxy/plugin loader path, not the compiled XFBIN data."
            : "Arm the probe first, fully close/relaunch the game, reproduce the issue, then check again.";
        return new("NOT_DETECTED", suffix, false, false);
    }

    public static IReadOnlyList<string> ExportDiagnostics(string gamePath, string exportRoot)
    {
        Directory.CreateDirectory(exportRoot);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var copied = new List<string>();
        var candidates = new[]
        {
            Path.Combine(gamePath, "console.log"),
            Path.Combine(gamePath, "imgui_log.txt"),
            Path.Combine(gamePath, "moddingapi", "console.log"),
            Path.Combine(gamePath, "moddingapi", "imgui_log.txt"),
            Path.Combine(gamePath, DllMainMarker),
            Path.Combine(gamePath, InitMarker),
            Path.Combine(gamePath, ArmMarker),
        };

        foreach (string src in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(src)) continue;
            string name = $"{Path.GetFileNameWithoutExtension(src)}_{stamp}{Path.GetExtension(src)}";
            string dst = Path.Combine(exportRoot, name);
            File.Copy(src, dst, true);
            copied.Add(dst);
        }

        ApiProbeStatus probe = GetProbeStatus(gamePath);
        string report = Path.Combine(exportRoot, $"api_runtime_diagnostics_{stamp}.txt");
        var sb = new StringBuilder();
        sb.AppendLine("NSC Mod Manager Android — UltimateStormAPI runtime diagnostics");
        sb.AppendLine("Time: " + DateTime.Now.ToString("O"));
        sb.AppendLine("Game: " + gamePath);
        sb.AppendLine("Probe state: " + probe.State);
        sb.AppendLine("Probe detail: " + probe.Message);
        sb.AppendLine();
        AppendFileInfo(sb, Path.Combine(gamePath, "d3dcompiler_47.dll"), "UltimateStormAPI proxy");
        AppendFileInfo(sb, Path.Combine(gamePath, "d3dcompiler_47_o.dll"), "Original D3DCompiler backup");
        AppendFileInfo(sb, Path.Combine(gamePath, "moddingapi", "mods", "base_game", "CPKLoader.dll"), "CPKLoader plugin");
        AppendFileInfo(sb, Path.Combine(gamePath, "moddingapi", "mods", "base_game", "NSCApiRuntimeProbe.dll"), "NSC runtime probe plugin");
        sb.AppendLine();

        string config = Path.Combine(gamePath, "moddingapi", "config.ini");
        sb.AppendLine("[config.ini]");
        if (File.Exists(config))
        {
            foreach (string line in File.ReadLines(config))
            {
                string t = line.Trim();
                if (t.StartsWith("enable_debug=", StringComparison.OrdinalIgnoreCase)
                    || t.StartsWith("enable_console=", StringComparison.OrdinalIgnoreCase))
                    sb.AppendLine(t);
            }
        }
        else sb.AppendLine("missing");

        foreach (string marker in new[] { ArmMarker, DllMainMarker, InitMarker })
        {
            string p = Path.Combine(gamePath, marker);
            sb.AppendLine();
            sb.AppendLine("[" + marker + "]");
            if (File.Exists(p)) sb.AppendLine(File.ReadAllText(p));
            else sb.AppendLine("missing");
        }

        File.WriteAllText(report, sb.ToString());
        copied.Add(report);
        return copied;
    }

    // Backward-compatible name used by older UI code; now always exports a
    // diagnostics report even when UltimateStormAPI itself produced no log.
    public static IReadOnlyList<string> ExportLogs(string gamePath, string exportRoot)
        => ExportDiagnostics(gamePath, exportRoot);

    static bool IsFresh(string path, DateTime armUtc)
    {
        if (!File.Exists(path)) return false;
        if (armUtc == DateTime.MinValue) return true;
        return File.GetLastWriteTimeUtc(path) >= armUtc.AddSeconds(-2);
    }

    static void AppendFileInfo(StringBuilder sb, string path, string label)
    {
        sb.AppendLine("[" + label + "]");
        sb.AppendLine("path=" + path);
        if (!File.Exists(path))
        {
            sb.AppendLine("exists=0");
            return;
        }
        var fi = new FileInfo(path);
        sb.AppendLine("exists=1");
        sb.AppendLine("size=" + fi.Length);
        try
        {
            using var stream = File.OpenRead(path);
            sb.AppendLine("sha256=" + Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant());
        }
        catch (Exception ex) { sb.AppendLine("sha256_error=" + ex.Message); }
    }

    static bool IsEnabled(string line, string key)
    {
        string trimmed = line.Trim();
        if (!trimmed.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase)) return false;
        return trimmed[(trimmed.IndexOf('=') + 1)..].Trim() == "1";
    }

    static void SetKey(string[] lines, string key, string value)
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
