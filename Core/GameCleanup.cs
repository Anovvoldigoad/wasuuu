using System.IO.Compression;

namespace NSC_ModManager_Android.Core;

public sealed record GameCleanupResult(
    int FilesRemoved,
    int BackupsRestored,
    int ApiFilesReset,
    int ApiFilesRemoved,
    int DirectoriesPruned)
{
    public string ClearSummary =>
        $"Game cleared: {FilesRemoved} generated file(s) removed, {BackupsRestored} backup(s) restored, ModdingAPI baseline reset ({ApiFilesReset} file(s)).";

    public string RemoveApiSummary =>
        $"ModdingAPI removed: {ApiFilesRemoved} payload/generated file(s) removed, {BackupsRestored} game backup(s) restored.";
}

/// <summary>
/// Owns cleanup of files created or overwritten by the Android compiler.
/// It deliberately avoids deleting the whole game/moddingapi directory so
/// unrelated user files are preserved.
/// </summary>
public static class GameCleanup
{
    private const string BackupSuffix = ".nscmm_android.bak";
    private const string ManagedManifestName = "nsc_android_managed_files.txt";

    private static readonly string[] GeneratedBaseGameNames =
    {
        "resources_modmanager.cpk", "resources_modmanager.cpk.info",
        "cpk_assets.cpk", "cpk_assets.cpk.info",
        "data_win32_modmanager.cpk", "data_win32_modmanager.cpk.info",
        "param_files.cpk", "param_files.cpk.info",
        "nsc_android_compile_report.txt", "nsc_android_last_error.txt"
    };

    private static readonly object ManifestLock = new();

    public static void RegisterManagedFile(string gamePath, string destination)
    {
        string gameRoot = NormalizeRoot(gamePath);
        string target = Path.GetFullPath(destination);
        if (!target.StartsWith(gameRoot, StringComparison.Ordinal))
            throw new InvalidDataException("Managed file escaped the game directory: " + destination);

        string relative = Path.GetRelativePath(gamePath, target)
            .Replace(Path.DirectorySeparatorChar, '/');
        if (relative.StartsWith("..", StringComparison.Ordinal))
            throw new InvalidDataException("Managed file escaped the game directory: " + destination);

        string manifest = GetManifestPath(gamePath);
        lock (ManifestLock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
            var existing = File.Exists(manifest)
                ? new HashSet<string>(File.ReadAllLines(manifest).Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (existing.Add(relative))
                File.AppendAllText(manifest, relative + System.Environment.NewLine);
        }
    }

    public static GameCleanupResult ClearCompiledMods(string payloadZip, string gamePath)
    {
        ValidateInputs(payloadZip, gamePath);
        int removed = 0, restored = 0, pruned = 0;

        ProcessGeneratedBaseGameFiles(gamePath, restorePriorVersion: false, ref removed, ref restored);
        ProcessManagedManifest(gamePath, ref removed, ref restored);
        RestoreRemainingBackups(gamePath, ref restored);
        RemoveControlFiles(gamePath, ref removed);

        // The semantic compiler overwrites ModdingAPI param baselines with merged
        // variants. Reinstalling the bundled payload resets those parameters while
        // keeping ModdingAPI itself installed.
        int reset = ModdingApiInstaller.Install(payloadZip, gamePath);
        pruned += PruneKnownEmptyDirectories(gamePath);
        return new GameCleanupResult(removed, restored, reset, 0, pruned);
    }

    public static GameCleanupResult RemoveModdingApi(string payloadZip, string gamePath)
    {
        ValidateInputs(payloadZip, gamePath);
        int removed = 0, restored = 0;

        // Removing ModdingAPI means generated CPKs and any previous generated-CPK
        // backups are intentionally discarded rather than restored.
        ProcessGeneratedBaseGameFiles(gamePath, restorePriorVersion: false, ref removed, ref restored);
        ProcessManagedManifest(gamePath, ref removed, ref restored);
        RestoreRemainingBackups(gamePath, ref restored);
        RemoveControlFiles(gamePath, ref removed);

        int apiRemoved = ModdingApiInstaller.Uninstall(payloadZip, gamePath);
        int pruned = PruneKnownEmptyDirectories(gamePath);
        return new GameCleanupResult(removed, restored, 0, apiRemoved, pruned);
    }

    private static void ProcessGeneratedBaseGameFiles(
        string gamePath,
        bool restorePriorVersion,
        ref int removed,
        ref int restored)
    {
        string baseGame = Path.Combine(gamePath, "moddingapi", "mods", "base_game");
        foreach (string name in GeneratedBaseGameNames)
        {
            string target = Path.Combine(baseGame, name);
            string backup = target + BackupSuffix;
            if (restorePriorVersion && File.Exists(backup))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(backup, target, true);
                File.Delete(backup);
                restored++;
            }
            else
            {
                if (File.Exists(target)) { File.Delete(target); removed++; }
                if (File.Exists(backup)) { File.Delete(backup); removed++; }
            }
        }
    }

    private static void ProcessManagedManifest(string gamePath, ref int removed, ref int restored)
    {
        string manifest = GetManifestPath(gamePath);
        if (!File.Exists(manifest)) return;

        string gameRoot = NormalizeRoot(gamePath);
        foreach (string raw in File.ReadAllLines(manifest))
        {
            string relative = raw.Trim();
            if (relative.Length == 0) continue;
            string target = Path.GetFullPath(Path.Combine(gamePath,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(gameRoot, StringComparison.Ordinal)) continue;

            string backup = target + BackupSuffix;
            if (File.Exists(backup))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(backup, target, true);
                File.Delete(backup);
                restored++;
            }
            else if (File.Exists(target))
            {
                File.Delete(target);
                removed++;
            }
        }

        File.Delete(manifest);
        removed++;
    }

    private static void RestoreRemainingBackups(string gamePath, ref int restored)
    {
        // Compatibility with Phase 2B/2C builds created before the managed-file
        // manifest existed. Backups were only created under these compiler-owned
        // areas, so avoid recursively scanning the entire (potentially huge) game.
        string[] roots =
        {
            Path.Combine(gamePath, "data", "ui", "flash", "OTHER"),
            Path.Combine(gamePath, "data", "system"),
            Path.Combine(gamePath, "moddingapi", "mods", "base_game")
        };

        foreach (string root in roots.Where(Directory.Exists))
        {
            foreach (string backup in Directory.EnumerateFiles(root, "*" + BackupSuffix, SearchOption.AllDirectories)
                         .OrderByDescending(x => x.Length))
            {
                string target = backup[..^BackupSuffix.Length];
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(backup, target, true);
                File.Delete(backup);
                restored++;
            }
        }
    }

    private static void RemoveControlFiles(string gamePath, ref int removed)
    {
        string baseGame = Path.Combine(gamePath, "moddingapi", "mods", "base_game");
        foreach (string name in new[] { ManagedManifestName, "nsc_android_compile_report.txt", "nsc_android_last_error.txt" })
        {
            string path = Path.Combine(baseGame, name);
            if (File.Exists(path)) { File.Delete(path); removed++; }
        }
    }

    private static int PruneKnownEmptyDirectories(string gamePath)
    {
        string[] dirs =
        {
            Path.Combine(gamePath, "moddingapi", "mods", "base_game"),
            Path.Combine(gamePath, "moddingapi", "mods"),
            Path.Combine(gamePath, "moddingapi", "param", "NSC"),
            Path.Combine(gamePath, "moddingapi", "param", "NS4"),
            Path.Combine(gamePath, "moddingapi", "param"),
            Path.Combine(gamePath, "moddingapi")
        };
        int count = 0;
        foreach (string dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            if (Directory.EnumerateFileSystemEntries(dir).Any()) continue;
            Directory.Delete(dir, false);
            count++;
        }
        return count;
    }

    private static string GetManifestPath(string gamePath)
        => Path.Combine(gamePath, "moddingapi", "mods", "base_game", ManagedManifestName);

    private static string NormalizeRoot(string path)
        => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

    private static void ValidateInputs(string payloadZip, string gamePath)
    {
        if (!Directory.Exists(gamePath))
            throw new DirectoryNotFoundException("Game directory is not accessible.");
        if (!File.Exists(payloadZip))
            throw new FileNotFoundException("Bundled ModdingAPI payload is missing.", payloadZip);
    }
}
