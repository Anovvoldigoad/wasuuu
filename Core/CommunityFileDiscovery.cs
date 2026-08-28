namespace NSC_ModManager_Android.Core;

/// <summary>
/// Community mod archives are authored on Windows but compiled on Android/Linux.
/// Resolve convention-based filenames case-insensitively so casing differences in
/// ZIPs do not turn into missing features on a case-sensitive filesystem.
/// </summary>
internal static class CommunityFileDiscovery
{
    internal static IEnumerable<string> EnumerateNamed(string root, string fileName)
    {
        if (!Directory.Exists(root)) return Array.Empty<string>();
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path).Equals(fileName, StringComparison.OrdinalIgnoreCase));
    }

    internal static IEnumerable<string> EnumerateExtension(string root, string extension)
    {
        if (!Directory.Exists(root)) return Array.Empty<string>();
        if (!extension.StartsWith('.')) extension = "." + extension;
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase));
    }
}
