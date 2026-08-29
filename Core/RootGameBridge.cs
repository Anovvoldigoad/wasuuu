using System.IO.Compression;
using System.Security.Cryptography;

namespace NSC_ModManager_Android.Core;

/// <summary>
/// Root-backed game target used for Winlator private C: drive. Compilation
/// still happens unprivileged in app cache; only minimal source reads and final
/// file commits use su. The full game is never copied to temporary storage.
/// </summary>
public static class RootGameBridge
{
    const string BackupSuffix = ".nscmm_android.bak";
    const string ManagedManifestName = "nsc_android_managed_files.txt";

    static readonly HashSet<string> GeneratedBaseGameNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "resources_modmanager.cpk", "resources_modmanager.cpk.info",
        "cpk_assets.cpk", "cpk_assets.cpk.info",
        "data_win32_modmanager.cpk", "data_win32_modmanager.cpk.info",
        "param_files.cpk", "param_files.cpk.info",
        "nsc_android_compile_report.txt", "nsc_android_last_error.txt"
    };

    static readonly string[] OwnedDiagnosticDlls =
    {
        "NSCApiConditionCompatFix_v1.dll",
        "NSCApiSpecialDPadAwakeningTrace_v1.dll",
        "NSCApiHookProbe.dll",
        "NSCApiActionTrace.dll",
        "NSCApiActionTrace_v3.dll",
        "NSCApiDeepTrace_v4.dll",
        "NSCApiRuntimeStateTrace_v5.dll",
        "NSCApiDPadGateTrace_v6.dll",
        "NSCApiDPadInternalTrace_v7.dll",
        "NSCApiDPadInternalTrace_v7_1.dll"
    };

    public static CompileResult Compile(
        AndroidCompiler compiler,
        string rootGamePath,
        string modsPath,
        string payloadZip,
        string baseParamZip,
        string messageBaseZip,
        string outerWorkRoot,
        int appUid,
        Action<string>? progress = null)
    {
        progress ??= _ => { };
        EnsureRootPath(rootGamePath);
        string owner = RootShell.GetOwner(rootGamePath);

        if (Directory.Exists(outerWorkRoot)) Directory.Delete(outerWorkRoot, true);
        Directory.CreateDirectory(outerWorkRoot);
        string shadow = Path.Combine(outerWorkRoot, "game_shadow");
        string innerWork = Path.Combine(outerWorkRoot, "compiler_work");
        Directory.CreateDirectory(Path.Combine(shadow, "data")); // PathValidator marker.

        // Shader is the only vanilla game asset currently read during semantic
        // compilation. Pull just this file if it exists; never mirror the game.
        string rootShader = RootShell.CombineUnix(rootGamePath, "data/system/nuccMaterial_dx11.nsh");
        string shadowShader = Path.Combine(shadow, "data", "system", "nuccMaterial_dx11.nsh");
        if (RootShell.FileExists(rootShader))
        {
            progress("Root mode: reading shader baseline from Winlator C:...");
            RootShell.CopyFromRoot(rootShader, shadowShader, appUid);
        }

        Dictionary<string, string> before = Snapshot(shadow);
        CompileResult result = compiler.Compile(shadow, modsPath, payloadZip, baseParamZip, messageBaseZip, innerWork,
            m => progress("[ROOT] " + m));

        // Report the real target rather than the staging directory.
        if (!string.IsNullOrWhiteSpace(result.ReportPath) && File.Exists(result.ReportPath))
        {
            string report = File.ReadAllText(result.ReportPath);
            report = report.Replace("Game: " + shadow, "Game: " + rootGamePath + System.Environment.NewLine + "Root Winlator mode: 1", StringComparison.Ordinal);
            File.WriteAllText(result.ReportPath, report);
        }

        Dictionary<string, string> after = Snapshot(shadow);
        string[] changed = after.Where(kv => !before.TryGetValue(kv.Key, out string? oldHash) || !oldHash.Equals(kv.Value, StringComparison.Ordinal))
            .Select(kv => kv.Key).OrderBy(x => x.EndsWith(BackupSuffix, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

        progress($"Root mode: committing {changed.Length} generated/runtime file(s) to Winlator C:...");
        var changedSet = new HashSet<string>(changed, StringComparer.OrdinalIgnoreCase);
        foreach (string rel in changed)
        {
            string source = Path.Combine(shadow, rel.Replace('/', Path.DirectorySeparatorChar));
            string destination = RootShell.CombineUnix(rootGamePath, rel);

            if (rel.EndsWith(BackupSuffix, StringComparison.OrdinalIgnoreCase))
            {
                // Never replace an older backup: it must remain the true vanilla/
                // pre-compiler state from the first install.
                if (!RootShell.FileExists(destination)) RootShell.CopyToRoot(source, destination, owner);
                continue;
            }

            if (ShouldBackup(rel))
            {
                string backupRel = rel + BackupSuffix;
                string backup = destination + BackupSuffix;
                if (!changedSet.Contains(backupRel) && RootShell.FileExists(destination) && !RootShell.FileExists(backup))
                    RootShell.CopyRootToRoot(destination, backup, owner);
            }
            RootShell.CopyToRoot(source, destination, owner);
        }

        RemoveOwnedDiagnostics(rootGamePath);
        VerifyConditionFix(rootGamePath);
        result.ReportPath = RootShell.CombineUnix(rootGamePath, "moddingapi/mods/base_game/nsc_android_compile_report.txt");
        progress("Root mode: compile commit finished.");
        return result;
    }

    public static int InstallApi(string payloadZip, string rootGamePath, string workRoot)
    {
        EnsureRootPath(rootGamePath);
        string owner = RootShell.GetOwner(rootGamePath);
        if (Directory.Exists(workRoot)) Directory.Delete(workRoot, true);
        Directory.CreateDirectory(workRoot);
        string shadow = Path.Combine(workRoot, "game_shadow");
        Directory.CreateDirectory(shadow);
        int count = ModdingApiInstaller.Install(payloadZip, shadow);
        CommitWholeShadow(shadow, rootGamePath, owner);
        RemoveOwnedDiagnostics(rootGamePath);
        VerifyConditionFix(rootGamePath);
        return count;
    }

    public static GameCleanupResult ClearCompiledMods(string payloadZip, string rootGamePath, string workRoot)
    {
        EnsureRootPath(rootGamePath);
        string owner = RootShell.GetOwner(rootGamePath);
        int removed = 0, restored = 0;

        string baseGame = RootShell.CombineUnix(rootGamePath, "moddingapi/mods/base_game");
        foreach (string name in GeneratedBaseGameNames)
        {
            foreach (string p in new[] { RootShell.CombineUnix(baseGame, name), RootShell.CombineUnix(baseGame, name + BackupSuffix) })
                if (RootShell.FileExists(p)) { RootShell.DeleteFile(p); removed++; }
        }

        string manifest = RootShell.CombineUnix(baseGame, ManagedManifestName);
        if (RootShell.FileExists(manifest))
        {
            foreach (string raw in RootShell.ReadText(manifest).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!IsSafeRelative(raw)) continue;
                string target = RootShell.CombineUnix(rootGamePath, raw);
                string backup = target + BackupSuffix;
                if (RootShell.FileExists(backup))
                {
                    RootShell.CopyRootToRoot(backup, target, owner);
                    RootShell.DeleteFile(backup);
                    restored++;
                }
                else if (RootShell.FileExists(target)) { RootShell.DeleteFile(target); removed++; }
            }
            RootShell.DeleteFile(manifest); removed++;
        }

        restored += RestoreRemainingBackups(rootGamePath, owner);
        foreach (string name in new[] { ManagedManifestName, "nsc_android_compile_report.txt", "nsc_android_last_error.txt" })
        {
            string p = RootShell.CombineUnix(baseGame, name);
            if (RootShell.FileExists(p)) { RootShell.DeleteFile(p); removed++; }
        }

        int reset = InstallApi(payloadZip, rootGamePath, workRoot);
        PruneKnownEmptyDirectories(rootGamePath);
        return new GameCleanupResult(removed, restored, reset, 0, 0);
    }

    public static GameCleanupResult RemoveModdingApi(string payloadZip, string rootGamePath)
    {
        EnsureRootPath(rootGamePath);
        string owner = RootShell.GetOwner(rootGamePath);
        int removed = 0, restored = 0, apiRemoved = 0;

        string baseGame = RootShell.CombineUnix(rootGamePath, "moddingapi/mods/base_game");
        foreach (string name in GeneratedBaseGameNames)
        {
            foreach (string p in new[] { RootShell.CombineUnix(baseGame, name), RootShell.CombineUnix(baseGame, name + BackupSuffix) })
                if (RootShell.FileExists(p)) { RootShell.DeleteFile(p); removed++; }
        }

        string manifest = RootShell.CombineUnix(baseGame, ManagedManifestName);
        if (RootShell.FileExists(manifest))
        {
            foreach (string raw in RootShell.ReadText(manifest).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!IsSafeRelative(raw)) continue;
                string target = RootShell.CombineUnix(rootGamePath, raw);
                string backup = target + BackupSuffix;
                if (RootShell.FileExists(backup))
                {
                    RootShell.CopyRootToRoot(backup, target, owner);
                    RootShell.DeleteFile(backup);
                    restored++;
                }
                else if (RootShell.FileExists(target)) { RootShell.DeleteFile(target); removed++; }
            }
            RootShell.DeleteFile(manifest); removed++;
        }
        restored += RestoreRemainingBackups(rootGamePath, owner);

        using (var archive = ZipFile.OpenRead(payloadZip))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                string rel = entry.FullName.Replace('\\', '/');
                if (!IsSafeRelative(rel)) continue;
                if (rel.Equals("d3dcompiler_47.dll", StringComparison.OrdinalIgnoreCase)
                    || rel.Equals("d3dcompiler_47_o.dll", StringComparison.OrdinalIgnoreCase)) continue;
                string target = RootShell.CombineUnix(rootGamePath, rel);
                if (RootShell.FileExists(target)) { RootShell.DeleteFile(target); apiRemoved++; }
            }
        }

        string d3d = RootShell.CombineUnix(rootGamePath, "d3dcompiler_47.dll");
        string d3dBackup = RootShell.CombineUnix(rootGamePath, "d3dcompiler_47_o.dll");
        if (RootShell.FileExists(d3dBackup))
        {
            if (RootShell.FileExists(d3d)) { RootShell.DeleteFile(d3d); apiRemoved++; }
            RootShell.MoveRoot(d3dBackup, d3d, owner); apiRemoved++;
        }
        else if (RootShell.FileExists(d3d)) { RootShell.DeleteFile(d3d); apiRemoved++; }

        RemoveOwnedDiagnostics(rootGamePath);
        PruneKnownEmptyDirectories(rootGamePath);
        return new GameCleanupResult(removed, restored, 0, apiRemoved, 0);
    }

    public static bool IsConditionFixPresent(string rootGamePath)
    {
        string path = RootShell.CombineUnix(rootGamePath, "moddingapi/mods/base_game/NSCApiConditionCompatFix_v2.dll");
        if (!RootShell.FileExists(path)) return false;
        string hash = RootShell.Sha256(path);
        return hash.Equals(ModdingApiInstaller.ConditionCompatFixSha256, StringComparison.OrdinalIgnoreCase);
    }

    public static string WriteCompileError(string rootGamePath, Exception ex, string localTemp)
    {
        EnsureRootPath(rootGamePath);
        string owner = RootShell.GetOwner(rootGamePath);
        string target = RootShell.CombineUnix(rootGamePath, "moddingapi/mods/base_game/nsc_android_last_error.txt");
        string text = "NSC Mod Manager Android — v0.5.3 root compile error" + System.Environment.NewLine +
                      "Time: " + DateTime.Now.ToString("O") + System.Environment.NewLine +
                      "App: 0.5.3" + System.Environment.NewLine +
                      "Game: " + rootGamePath + System.Environment.NewLine +
                      "Root Winlator mode: 1" + System.Environment.NewLine + System.Environment.NewLine + ex;
        RootShell.WriteText(target, text, owner, localTemp);
        return target;
    }

    static void RemoveOwnedDiagnostics(string rootGamePath)
    {
        string baseGame = RootShell.CombineUnix(rootGamePath, "moddingapi/mods/base_game");
        foreach (string name in OwnedDiagnosticDlls)
        {
            string path = RootShell.CombineUnix(baseGame, name);
            if (RootShell.FileExists(path)) RootShell.DeleteFile(path);
        }
    }

    static void VerifyConditionFix(string rootGamePath)
    {
        if (!IsConditionFixPresent(rootGamePath))
            throw new InvalidDataException("SC 1.70 condition compatibility fix failed root-mode integrity verification after install.");
    }

    static void CommitWholeShadow(string shadow, string rootGamePath, string owner)
    {
        foreach (string source in Directory.EnumerateFiles(shadow, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(shadow, source).Replace(Path.DirectorySeparatorChar, '/');
            if (!IsSafeRelative(rel)) throw new InvalidDataException("Root staging file escaped its shadow root.");
            RootShell.CopyToRoot(source, RootShell.CombineUnix(rootGamePath, rel), owner);
        }
    }

    static int RestoreRemainingBackups(string rootGamePath, string owner)
    {
        int restored = 0;
        foreach (string relRoot in new[] { "data/ui/flash/OTHER", "data/system", "moddingapi/mods/base_game" })
        {
            string root = RootShell.CombineUnix(rootGamePath, relRoot);
            foreach (string backup in RootShell.FindBackups(root))
            {
                string target = backup[..^BackupSuffix.Length];
                RootShell.CopyRootToRoot(backup, target, owner);
                RootShell.DeleteFile(backup);
                restored++;
            }
        }
        return restored;
    }

    static void PruneKnownEmptyDirectories(string rootGamePath)
    {
        foreach (string rel in new[] { "moddingapi/mods/base_game", "moddingapi/mods", "moddingapi/param/NSC", "moddingapi/param/NS4", "moddingapi/param", "moddingapi" })
            RootShell.RemoveDirectoryIfEmpty(RootShell.CombineUnix(rootGamePath, rel));
    }

    static Dictionary<string, string> Snapshot(string root)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(root)) return result;
        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            using var stream = File.OpenRead(file);
            result[rel] = Convert.ToHexString(SHA256.HashData(stream));
        }
        return result;
    }

    static bool ShouldBackup(string rel)
    {
        string normalized = rel.Replace('\\', '/');
        if (normalized.Equals("data/system/nuccMaterial_dx11.nsh", StringComparison.OrdinalIgnoreCase)) return true;
        if (normalized.StartsWith("data/ui/flash/OTHER/", StringComparison.OrdinalIgnoreCase)) return true;
        if (normalized.StartsWith("moddingapi/mods/base_game/", StringComparison.OrdinalIgnoreCase))
            return GeneratedBaseGameNames.Contains(Path.GetFileName(normalized));
        return false;
    }

    static bool IsSafeRelative(string rel)
    {
        if (string.IsNullOrWhiteSpace(rel)) return false;
        string normalized = rel.Replace('\\', '/');
        return !normalized.StartsWith("/", StringComparison.Ordinal)
               && !normalized.Equals("..", StringComparison.Ordinal)
               && !normalized.StartsWith("../", StringComparison.Ordinal)
               && !normalized.Contains("/../", StringComparison.Ordinal);
    }

    static void EnsureRootPath(string rootGamePath)
    {
        if (!RootShell.IsAvailable(out string detail))
            throw new UnauthorizedAccessException("Root access is unavailable. Grant NSC Mod Manager permanent root permission in Magisk/KernelSU. " + detail);
        var check = RootShell.ValidateGamePath(rootGamePath);
        if (!check.Ok) throw new DirectoryNotFoundException(check.Message);
    }
}
