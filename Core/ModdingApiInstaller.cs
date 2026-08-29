using System.IO.Compression;
using System.Security.Cryptography;

namespace NSC_ModManager_Android.Core;

public static class ModdingApiInstaller
{
    public const string ConditionCompatFixEntry = "moddingapi/mods/base_game/NSCApiConditionCompatFix_v1.dll";
    public const string ConditionCompatFixSha256 = "e5ee5617a8c17f6431c34db6ace1a1ffcb1c8339735adb60471a73e0414983fa";

    private static readonly string[] OwnedDiagnosticDlls =
    {
        "NSCApiHookProbe.dll",
        "NSCApiActionTrace.dll",
        "NSCApiActionTrace_v3.dll",
        "NSCApiDeepTrace_v4.dll",
        "NSCApiRuntimeStateTrace_v5.dll",
        "NSCApiDPadGateTrace_v6.dll",
        "NSCApiDPadInternalTrace_v7.dll",
        "NSCApiDPadInternalTrace_v7_1.dll"
    };

    public static int Install(string payloadZip, string gamePath)
    {
        if (!File.Exists(payloadZip))
            throw new FileNotFoundException("Bundled ModdingAPI payload is missing.", payloadZip);
        Directory.CreateDirectory(gamePath);

        string root = Path.GetFullPath(gamePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        int count = 0;
        using var archive = ZipFile.OpenRead(payloadZip);
        ValidateConditionCompatPayload(archive);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string target = Path.GetFullPath(Path.Combine(gamePath, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(root, StringComparison.Ordinal))
                throw new InvalidDataException("Unsafe path in ModdingAPI payload: " + entry.FullName);

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            string? dir = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            entry.ExtractToFile(target, overwrite: true);
            count++;
        }

        RemoveOwnedDiagnostics(gamePath);
        if (!IsConditionCompatFixInstalled(gamePath))
            throw new InvalidDataException("SC 1.70 condition compatibility fix failed integrity verification after install.");

        return count;
    }

    public static string GetConditionCompatFixPath(string gamePath) =>
        Path.Combine(gamePath, "moddingapi", "mods", "base_game", "NSCApiConditionCompatFix_v1.dll");

    public static bool IsConditionCompatFixInstalled(string gamePath)
    {
        string path = GetConditionCompatFixPath(gamePath);
        if (!File.Exists(path)) return false;
        try
        {
            using var stream = File.OpenRead(path);
            string hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            return hash.Equals(ConditionCompatFixSha256, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void ValidateConditionCompatPayload(ZipArchive archive)
    {
        ZipArchiveEntry? fix = archive.GetEntry(ConditionCompatFixEntry);
        if (fix is null || fix.Length == 0)
            throw new FileNotFoundException("Bundled SC 1.70 condition compatibility fix is missing.", ConditionCompatFixEntry);

        using var stream = fix.Open();
        string hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (!hash.Equals(ConditionCompatFixSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Bundled SC 1.70 condition compatibility fix failed SHA-256 verification.");
    }

    private static void RemoveOwnedDiagnostics(string gamePath)
    {
        string baseGame = Path.Combine(gamePath, "moddingapi", "mods", "base_game");
        foreach (string name in OwnedDiagnosticDlls)
        {
            string path = Path.Combine(baseGame, name);
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* Diagnostic cleanup must never block a valid runtime install. */ }
        }
    }

    public static string InstallRuntimeProbe(string payloadZip, string gamePath)
    {
        if (!File.Exists(payloadZip))
            throw new FileNotFoundException("Bundled ModdingAPI payload is missing.", payloadZip);

        const string probeEntry = "moddingapi/mods/base_game/NSCApiRuntimeProbe.dll";
        using var archive = ZipFile.OpenRead(payloadZip);
        ZipArchiveEntry? entry = archive.GetEntry(probeEntry);
        if (entry is null || entry.Length == 0)
            throw new FileNotFoundException("Runtime probe is missing from the bundled ModdingAPI payload.", probeEntry);

        string root = Path.GetFullPath(gamePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string target = Path.GetFullPath(Path.Combine(gamePath, probeEntry.Replace('/', Path.DirectorySeparatorChar)));
        if (!target.StartsWith(root, StringComparison.Ordinal))
            throw new InvalidDataException("Unsafe runtime probe target path.");

        string? dir = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        entry.ExtractToFile(target, overwrite: true);
        return target;
    }

    public static int Uninstall(string payloadZip, string gamePath)
    {
        if (!File.Exists(payloadZip))
            throw new FileNotFoundException("Bundled ModdingAPI payload is missing.", payloadZip);
        if (!Directory.Exists(gamePath)) return 0;

        string root = Path.GetFullPath(gamePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string d3d = Path.Combine(gamePath, "d3dcompiler_47.dll");
        string d3dBackup = Path.Combine(gamePath, "d3dcompiler_47_o.dll");
        int count = 0;
        var directories = new List<string>();
        using var archive = ZipFile.OpenRead(payloadZip);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string normalizedEntry = entry.FullName.Replace('\\', '/');
            string target = Path.GetFullPath(Path.Combine(gamePath, normalizedEntry.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(root, StringComparison.Ordinal))
                throw new InvalidDataException("Unsafe path in ModdingAPI payload: " + entry.FullName);

            if (string.IsNullOrEmpty(entry.Name))
            {
                directories.Add(target.TrimEnd(Path.DirectorySeparatorChar));
                continue;
            }

            // Desktop NSC ModManager restores the vanilla d3dcompiler backup when
            // ModdingAPI is removed. Do not delete these two entries generically.
            if (normalizedEntry.Equals("d3dcompiler_47.dll", StringComparison.OrdinalIgnoreCase)
                || normalizedEntry.Equals("d3dcompiler_47_o.dll", StringComparison.OrdinalIgnoreCase))
                continue;

            if (File.Exists(target))
            {
                File.Delete(target);
                count++;
            }
        }

        if (File.Exists(d3dBackup))
        {
            if (File.Exists(d3d)) { File.Delete(d3d); count++; }
            File.Move(d3dBackup, d3d, true);
            count++;
        }
        else if (File.Exists(d3d))
        {
            File.Delete(d3d);
            count++;
        }

        foreach (string dir in directories.Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(x => x.Length))
        {
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir, false);
        }
        return count;
    }
}
