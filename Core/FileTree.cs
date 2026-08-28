namespace NSC_ModManager_Android.Core;

public static class FileTree
{
    public static int CopyAll(string source, string destination, bool overwrite = true)
    {
        if (!Directory.Exists(source)) return 0;
        int count = 0;
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file);
            string target = Path.Combine(destination, relative);
            string? dir = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.Copy(file, target, overwrite);
            count++;
        }
        return count;
    }

    public static int CopyResourceFiles(string source, string destination, CompileResult result)
    {
        if (!Directory.Exists(source)) return 0;
        int copied = 0;
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string ext = Path.GetExtension(file);
            if (!ext.Equals(".xfbin", StringComparison.OrdinalIgnoreCase) &&
                !ext.Equals(".acb", StringComparison.OrdinalIgnoreCase) &&
                !ext.Equals(".awb", StringComparison.OrdinalIgnoreCase))
                continue;

            if (ext.Equals(".xfbin", StringComparison.OrdinalIgnoreCase) && XfbinPreflight.IsParameterXfbin(file))
            {
                result.ParameterXfbinsDetected++;
                if (!XfbinPreflight.TryReadHeader(file, out var header, out string error))
                    result.Warnings.Add($"Invalid parameter XFBIN '{Path.GetFileName(file)}': {error}");
                else if (header.Version == 0)
                    result.Warnings.Add($"Parameter XFBIN '{Path.GetFileName(file)}' has version 0 and was not staged.");
                continue;
            }

            string relative = NormalizeNscSoundPath(Path.GetRelativePath(source, file));
            string target = Path.Combine(destination, relative);
            string? dir = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.Copy(file, target, true);
            copied++;
        }
        return copied;
    }

    public static bool HasFiles(string path) =>
        Directory.Exists(path) && Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Any();

    private static string NormalizeNscSoundPath(string relative)
    {
        string[] segments = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < segments.Length; i++)
            if (segments[i].Equals("sndev", StringComparison.OrdinalIgnoreCase))
                segments[i] = "SndEvent";
        return segments.Length == 0 ? relative : Path.Combine(segments);
    }
}
