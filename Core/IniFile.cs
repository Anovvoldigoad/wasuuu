using System.Text;

namespace NSC_ModManager_Android.Core;

public sealed class IniFile
{
    public string Path { get; }
    private readonly object _gate = new();

    public IniFile(string path) => Path = path;

    public string Read(string key, string section = "ModManager", string defaultValue = "")
    {
        lock (_gate)
        {
            if (!File.Exists(Path)) return defaultValue;
            string current = "";
            foreach (string raw in File.ReadLines(Path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    current = line[1..^1].Trim();
                    continue;
                }
                if (!current.Equals(section, StringComparison.OrdinalIgnoreCase)) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line[..eq].Trim();
                if (!k.Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
                return line[(eq + 1)..].Trim();
            }
            return defaultValue;
        }
    }

    public void Write(string key, string value, string section = "ModManager")
    {
        lock (_gate)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path) ?? ".");
            var sections = Parse();
            if (!sections.TryGetValue(section, out var values))
                sections[section] = values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            values[key] = value ?? "";

            using var writer = new StreamWriter(Path, false, new UTF8Encoding(false));
            foreach (var sec in sections)
            {
                writer.WriteLine($"[{sec.Key}]");
                foreach (var item in sec.Value)
                    writer.WriteLine($"{item.Key}={item.Value}");
                writer.WriteLine();
            }
        }
    }

    private Dictionary<string, Dictionary<string, string>> Parse()
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(Path)) return result;
        string current = "ModManager";
        foreach (string raw in File.ReadLines(Path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                current = line[1..^1].Trim();
                if (!result.ContainsKey(current))
                    result[current] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            if (!result.TryGetValue(current, out var values))
                result[current] = values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            values[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }
        return result;
    }
}
