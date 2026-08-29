using System.Diagnostics;
using System.Text;

namespace NSC_ModManager_Android.Core;

/// <summary>
/// Small Magisk/KernelSU-compatible root shell bridge. It is intentionally
/// command-based so the normal compiler can remain unprivileged while only
/// private Winlator game-folder reads/writes cross the root boundary.
/// </summary>
public static class RootShell
{
    public static bool IsAvailable(out string detail)
    {
        try
        {
            var result = Run("id", 6000);
            detail = result.Output.Trim();
            return result.ExitCode == 0 && result.Output.Contains("uid=0", StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            detail = ex.Message;
            return false;
        }
    }

    public static IReadOnlyList<string> DetectWinlatorCDrives()
    {
        const string script = "for p in " +
            "/data/user/0/*/files/imagefs/home/xuser-* " +
            "/data/user/0/*/files/imagefs/home/xuser " +
            "/data/user/0/*/files/images/home/xuser-* " +
            "/data/user/0/*/files/containers/* " +
            "/data/data/*/files/imagefs/home/xuser-* " +
            "/data/data/*/files/imagefs/home/xuser " +
            "/data/data/*/files/images/home/xuser-* " +
            "/data/data/*/files/containers/*; do " +
            "[ -d \"$p/.wine/drive_c\" ] && printf '%s\\n' \"$p/.wine/drive_c\"; done";
        var result = Run(script, 12000);
        if (result.ExitCode != 0 && string.IsNullOrWhiteSpace(result.Output))
            return Array.Empty<string>();
        return result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.EndsWith("/.wine/drive_c", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<string> ListDirectories(string path)
    {
        string q = Quote(path);
        var result = Run($"find {q} -mindepth 1 -maxdepth 1 -type d -print 2>/dev/null", 10000);
        if (result.ExitCode != 0 && string.IsNullOrWhiteSpace(result.Output))
            return Array.Empty<string>();
        return result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => Path.GetFileName(x), StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool DirectoryExists(string path) => Test("-d", path);
    public static bool FileExists(string path) => Test("-f", path);

    public static (bool Ok, string Message) ValidateGamePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return (false, "Root game path is empty.");
        if (!DirectoryExists(path)) return (false, "Root directory does not exist or root access was denied.");
        foreach (string marker in new[] { "NSUNSC.exe", "NSUNS4.exe", "data_win32", "data" })
        {
            string p = CombineUnix(path, marker);
            if (FileExists(p) || DirectoryExists(p))
                return (true, "Root game directory is accessible through su.");
        }
        return (true, "Root directory is accessible, but no known Storm marker was found.");
    }

    public static string ReadText(string path)
    {
        var result = Run($"cat {Quote(path)}", 12000);
        if (result.ExitCode != 0)
            throw new IOException("Root read failed: " + result.Error.Trim());
        return result.Output;
    }

    public static void CopyFromRoot(string source, string destination, int appUid)
    {
        string? dir = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        string command = $"cp -f {Quote(source)} {Quote(destination)} && chown {appUid}:{appUid} {Quote(destination)} && chmod 600 {Quote(destination)}";
        Ensure(command, "Root copy-to-app failed");
    }

    public static void CopyToRoot(string source, string destination, string owner)
    {
        string parent = UnixDirName(destination);
        EnsureDirectory(parent, owner);
        string command = $"cp -f {Quote(source)} {Quote(destination)} && chown {owner} {Quote(destination)} && chmod 600 {Quote(destination)}; " +
                         $"chcon --reference={Quote(parent)} {Quote(destination)} 2>/dev/null || true";
        Ensure(command, "Root copy-to-game failed");
    }

    public static void CopyRootToRoot(string source, string destination, string owner)
    {
        string parent = UnixDirName(destination);
        EnsureDirectory(parent, owner);
        string command = $"cp -pf {Quote(source)} {Quote(destination)} && chown {owner} {Quote(destination)}; " +
                         $"chcon --reference={Quote(parent)} {Quote(destination)} 2>/dev/null || true";
        Ensure(command, "Root backup copy failed");
    }

    public static void MoveRoot(string source, string destination, string owner)
    {
        string parent = UnixDirName(destination);
        EnsureDirectory(parent, owner);
        Ensure($"mv -f {Quote(source)} {Quote(destination)} && chown {owner} {Quote(destination)}", "Root move failed");
    }

    public static void DeleteFile(string path)
        => Ensure($"rm -f {Quote(path)}", "Root delete failed");

    public static void DeleteTree(string path)
        => Ensure($"rm -rf {Quote(path)}", "Root tree delete failed");

    public static void RemoveDirectoryIfEmpty(string path)
        => Run($"rmdir {Quote(path)} 2>/dev/null || true", 5000);

    public static void WriteText(string destination, string text, string owner, string localTemp)
    {
        string? dir = Path.GetDirectoryName(localTemp);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(localTemp, text);
        try { CopyToRoot(localTemp, destination, owner); }
        finally { try { File.Delete(localTemp); } catch { } }
    }

    public static IReadOnlyList<string> FindBackups(string root)
    {
        if (!DirectoryExists(root)) return Array.Empty<string>();
        var result = Run($"find {Quote(root)} -type f -name '*.nscmm_android.bak' -print 2>/dev/null", 15000);
        return result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal).OrderByDescending(x => x.Length).ToArray();
    }

    public static string GetOwner(string path)
    {
        var result = Run($"stat -c '%u:%g' {Quote(path)} 2>/dev/null", 6000);
        string owner = result.Output.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        if (result.ExitCode != 0 || owner.Length == 0 || !owner.Contains(':'))
            throw new IOException("Unable to determine Winlator game-folder owner through root.");
        return owner;
    }

    public static void EnsureDirectory(string path, string owner)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/") return;
        // mkdir -p can create multiple levels as root. Chown every directory below
        // the closest existing parent by applying ownership to the final tree path.
        Ensure($"mkdir -p {Quote(path)} && chown {owner} {Quote(path)} && chmod 755 {Quote(path)}", "Root mkdir failed");
    }

    public static string Sha256(string path)
    {
        var result = Run($"sha256sum {Quote(path)} 2>/dev/null", 10000);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output)) return string.Empty;
        return result.Output.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
    }

    public static RootCommandResult Run(string command, int timeoutMs = 10000)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "su",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        process.StartInfo.ArgumentList.Add("-c");
        process.StartInfo.ArgumentList.Add(command);
        if (!process.Start()) throw new IOException("Unable to start su.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(timeoutMs))
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException("Root command timed out.");
        }
        return new RootCommandResult(process.ExitCode, output, error);
    }

    static bool Test(string flag, string path)
    {
        try { return Run($"test {flag} {Quote(path)}", 5000).ExitCode == 0; }
        catch { return false; }
    }

    static void Ensure(string command, string label)
    {
        var result = Run(command, 15000);
        if (result.ExitCode != 0)
            throw new IOException($"{label} (exit {result.ExitCode}): {result.Error.Trim()}");
    }

    public static string Quote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    public static string CombineUnix(string root, string relative)
        => root.TrimEnd('/') + "/" + relative.Replace('\\', '/').TrimStart('/');

    public static string UnixDirName(string path)
    {
        string value = path.TrimEnd('/');
        int slash = value.LastIndexOf('/');
        return slash <= 0 ? "/" : value[..slash];
    }
}

public sealed record RootCommandResult(int ExitCode, string Output, string Error);
