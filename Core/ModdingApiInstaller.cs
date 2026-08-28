using System.IO.Compression;

namespace NSC_ModManager_Android.Core;

public static class ModdingApiInstaller
{
    public static int Install(string payloadZip, string gamePath)
    {
        if (!File.Exists(payloadZip))
            throw new FileNotFoundException("Bundled ModdingAPI payload is missing.", payloadZip);
        Directory.CreateDirectory(gamePath);

        string root = Path.GetFullPath(gamePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        int count = 0;
        using var archive = ZipFile.OpenRead(payloadZip);
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
        return count;
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
