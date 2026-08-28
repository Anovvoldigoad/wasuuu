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
}
