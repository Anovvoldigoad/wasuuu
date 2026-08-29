using Android.Content;
using AndroidUri = Android.Net.Uri;
using System.IO.Compression;
using System.Security.Cryptography;

namespace NSC_ModManager_Android.Core;

/// <summary>
/// Direct no-root game target backed by an Android SAF document tree. This is
/// designed for Winlator DocumentsProvider exports of its private C: drive.
/// Compilation remains local in app cache and only required source files plus
/// generated outputs are streamed through ContentResolver.
/// </summary>
public static class SafGameBridge
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

    public static (bool Ok, string Message) ValidateGamePath(ContentResolver resolver, string treeUri)
    {
        try
        {
            SafDocumentTree tree = Open(resolver, treeUri);
            return tree.ValidateGameFolder();
        }
        catch (Exception ex) { return (false, "Cannot open SAF game folder: " + ex.Message); }
    }

    public static string GetDisplayName(ContentResolver resolver, string treeUri)
    {
        try { return Open(resolver, treeUri).RootDisplayName(); }
        catch { return "Winlator / SAF game folder"; }
    }

    public static (bool Ok, string Message) ProbeWriteAccess(ContentResolver resolver, string treeUri)
    {
        SafDocumentTree tree;
        try { tree = Open(resolver, treeUri); }
        catch (Exception ex) { return (false, "Cannot open selected SAF tree for writing: " + ex.Message); }

        string probe = "nscmm_saf_probe_" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            tree.WriteText(probe, "NSC Mod Manager SAF write probe");
            if (!tree.Exists(probe)) return (false, "DocumentsProvider accepted a write call but the probe file was not visible afterwards.");
            tree.DeleteFile(probe);
            return (true, "SAF read/write probe passed.");
        }
        catch (Exception ex)
        {
            try { tree.DeleteFile(probe); } catch { }
            return (false, "Selected DocumentsProvider is not writable enough for compilation: " + ex.Message);
        }
    }

    public static CompileResult Compile(
        AndroidCompiler compiler,
        ContentResolver resolver,
        string treeUri,
        string displayPath,
        string modsPath,
        string payloadZip,
        string baseParamZip,
        string messageBaseZip,
        string outerWorkRoot,
        Action<string>? progress = null)
    {
        progress ??= _ => { };
        SafDocumentTree tree = OpenValidated(resolver, treeUri);

        if (Directory.Exists(outerWorkRoot)) Directory.Delete(outerWorkRoot, true);
        Directory.CreateDirectory(outerWorkRoot);
        string shadow = Path.Combine(outerWorkRoot, "game_shadow");
        string innerWork = Path.Combine(outerWorkRoot, "compiler_work");
        Directory.CreateDirectory(Path.Combine(shadow, "data")); // PathValidator marker.

        const string shaderRel = "data/system/nuccMaterial_dx11.nsh";
        string shadowShader = Path.Combine(shadow, "data", "system", "nuccMaterial_dx11.nsh");
        if (tree.Exists(shaderRel))
        {
            progress("SAF mode: reading shader baseline from Winlator C:...");
            tree.CopyToLocal(shaderRel, shadowShader);
        }

        Dictionary<string, string> before = Snapshot(shadow);
        CompileResult result = compiler.Compile(shadow, modsPath, payloadZip, baseParamZip, messageBaseZip, innerWork,
            m => progress("[SAF] " + m));

        if (!string.IsNullOrWhiteSpace(result.ReportPath) && File.Exists(result.ReportPath))
        {
            string report = File.ReadAllText(result.ReportPath);
            report = report.Replace("Game: " + shadow,
                "Game: " + displayPath + System.Environment.NewLine +
                "Winlator SAF direct mode: 1" + System.Environment.NewLine +
                "Tree URI: " + treeUri,
                StringComparison.Ordinal);
            File.WriteAllText(result.ReportPath, report);
        }

        Dictionary<string, string> after = Snapshot(shadow);
        string[] changed = after.Where(kv => !before.TryGetValue(kv.Key, out string? oldHash) || !oldHash.Equals(kv.Value, StringComparison.Ordinal))
            .Select(kv => kv.Key)
            .OrderBy(x => x.EndsWith(BackupSuffix, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        progress($"SAF mode: streaming {changed.Length} generated/runtime file(s) directly to Winlator C:...");
        var changedSet = new HashSet<string>(changed, StringComparer.OrdinalIgnoreCase);
        foreach (string rel in changed)
        {
            string source = Path.Combine(shadow, rel.Replace('/', Path.DirectorySeparatorChar));
            if (rel.EndsWith(BackupSuffix, StringComparison.OrdinalIgnoreCase))
            {
                if (!tree.Exists(rel)) tree.CopyFromLocal(source, rel);
                continue;
            }

            if (ShouldBackup(rel))
            {
                string backupRel = rel + BackupSuffix;
                if (!changedSet.Contains(backupRel) && tree.Exists(rel) && !tree.Exists(backupRel))
                    tree.CopyRemote(rel, backupRel);
            }
            tree.CopyFromLocal(source, rel);
        }

        RemoveOwnedDiagnostics(tree);
        VerifyConditionFix(tree);
        result.ReportPath = "SAF:" + treeUri + "!/moddingapi/mods/base_game/nsc_android_compile_report.txt";
        progress("SAF mode: compile commit finished.");
        return result;
    }

    public static int InstallApi(ContentResolver resolver, string treeUri, string payloadZip, string workRoot)
    {
        SafDocumentTree tree = OpenValidated(resolver, treeUri);
        if (Directory.Exists(workRoot)) Directory.Delete(workRoot, true);
        Directory.CreateDirectory(workRoot);
        string shadow = Path.Combine(workRoot, "game_shadow");
        Directory.CreateDirectory(shadow);
        int count = ModdingApiInstaller.Install(payloadZip, shadow);
        CommitWholeShadow(tree, shadow);
        RemoveOwnedDiagnostics(tree);
        VerifyConditionFix(tree);
        return count;
    }

    public static GameCleanupResult ClearCompiledMods(ContentResolver resolver, string treeUri, string payloadZip, string workRoot)
    {
        SafDocumentTree tree = OpenValidated(resolver, treeUri);
        int removed = 0, restored = 0;
        const string baseGame = "moddingapi/mods/base_game";

        foreach (string name in GeneratedBaseGameNames)
        {
            foreach (string rel in new[] { baseGame + "/" + name, baseGame + "/" + name + BackupSuffix })
                if (tree.DeleteFile(rel)) removed++;
        }

        string manifest = baseGame + "/" + ManagedManifestName;
        if (tree.Exists(manifest))
        {
            foreach (string raw in tree.ReadText(manifest).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!IsSafeRelative(raw)) continue;
                string backup = raw + BackupSuffix;
                if (tree.Exists(backup))
                {
                    tree.CopyRemote(backup, raw);
                    tree.DeleteFile(backup);
                    restored++;
                }
                else if (tree.DeleteFile(raw)) removed++;
            }
            if (tree.DeleteFile(manifest)) removed++;
        }

        restored += RestoreRemainingBackups(tree);
        foreach (string name in new[] { ManagedManifestName, "nsc_android_compile_report.txt", "nsc_android_last_error.txt" })
            if (tree.DeleteFile(baseGame + "/" + name)) removed++;

        int reset = InstallApi(resolver, treeUri, payloadZip, workRoot);
        int pruned = PruneKnownEmptyDirectories(tree);
        return new GameCleanupResult(removed, restored, reset, 0, pruned);
    }

    public static GameCleanupResult RemoveModdingApi(ContentResolver resolver, string treeUri, string payloadZip)
    {
        SafDocumentTree tree = OpenValidated(resolver, treeUri);
        int removed = 0, restored = 0, apiRemoved = 0;
        const string baseGame = "moddingapi/mods/base_game";

        foreach (string name in GeneratedBaseGameNames)
        {
            foreach (string rel in new[] { baseGame + "/" + name, baseGame + "/" + name + BackupSuffix })
                if (tree.DeleteFile(rel)) removed++;
        }

        string manifest = baseGame + "/" + ManagedManifestName;
        if (tree.Exists(manifest))
        {
            foreach (string raw in tree.ReadText(manifest).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!IsSafeRelative(raw)) continue;
                string backup = raw + BackupSuffix;
                if (tree.Exists(backup))
                {
                    tree.CopyRemote(backup, raw);
                    tree.DeleteFile(backup);
                    restored++;
                }
                else if (tree.DeleteFile(raw)) removed++;
            }
            if (tree.DeleteFile(manifest)) removed++;
        }
        restored += RestoreRemainingBackups(tree);

        using (var archive = ZipFile.OpenRead(payloadZip))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                string rel = entry.FullName.Replace('\\', '/');
                if (!IsSafeRelative(rel)) continue;
                if (rel.Equals("d3dcompiler_47.dll", StringComparison.OrdinalIgnoreCase)
                    || rel.Equals("d3dcompiler_47_o.dll", StringComparison.OrdinalIgnoreCase)) continue;
                if (tree.DeleteFile(rel)) apiRemoved++;
            }
        }

        const string d3d = "d3dcompiler_47.dll";
        const string d3dBackup = "d3dcompiler_47_o.dll";
        if (tree.Exists(d3dBackup))
        {
            if (tree.DeleteFile(d3d)) apiRemoved++;
            tree.CopyRemote(d3dBackup, d3d);
            tree.DeleteFile(d3dBackup);
            apiRemoved += 2;
        }
        else if (tree.DeleteFile(d3d)) apiRemoved++;

        RemoveOwnedDiagnostics(tree);
        int pruned = PruneKnownEmptyDirectories(tree);
        return new GameCleanupResult(removed, restored, 0, apiRemoved, pruned);
    }

    public static bool IsConditionFixPresent(ContentResolver resolver, string treeUri)
    {
        try
        {
            SafDocumentTree tree = Open(resolver, treeUri);
            const string rel = "moddingapi/mods/base_game/NSCApiConditionCompatFix_v2.dll";
            return tree.Exists(rel) && tree.Sha256(rel).Equals(ModdingApiInstaller.ConditionCompatFixSha256, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static string WriteCompileError(ContentResolver resolver, string treeUri, string displayPath, Exception ex)
    {
        SafDocumentTree tree = OpenValidated(resolver, treeUri);
        const string rel = "moddingapi/mods/base_game/nsc_android_last_error.txt";
        string text = "NSC Mod Manager Android — v0.5.3 SAF compile error" + System.Environment.NewLine +
                      "Time: " + DateTime.Now.ToString("O") + System.Environment.NewLine +
                      "App: 0.5.3" + System.Environment.NewLine +
                      "Game: " + displayPath + System.Environment.NewLine +
                      "Winlator SAF direct mode: 1" + System.Environment.NewLine +
                      "Tree URI: " + treeUri + System.Environment.NewLine + System.Environment.NewLine + ex;
        tree.WriteText(rel, text);
        return "SAF:" + treeUri + "!/" + rel;
    }

    static SafDocumentTree Open(ContentResolver resolver, string treeUri)
    {
        if (string.IsNullOrWhiteSpace(treeUri)) throw new InvalidOperationException("No SAF game folder is saved. Use Select Folder and choose the Winlator game directory again.");
        AndroidUri uri = AndroidUri.Parse(treeUri) ?? throw new InvalidDataException("Saved SAF tree URI is invalid.");
        return new SafDocumentTree(resolver, uri);
    }

    static SafDocumentTree OpenValidated(ContentResolver resolver, string treeUri)
    {
        SafDocumentTree tree = Open(resolver, treeUri);
        var check = tree.ValidateGameFolder();
        if (!check.Ok) throw new UnauthorizedAccessException(check.Message + " Re-select the folder if Android revoked its persisted permission.");
        return tree;
    }

    static void CommitWholeShadow(SafDocumentTree tree, string shadow)
    {
        foreach (string source in Directory.EnumerateFiles(shadow, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(shadow, source).Replace(Path.DirectorySeparatorChar, '/');
            if (!IsSafeRelative(rel)) throw new InvalidDataException("SAF staging file escaped its shadow root.");
            tree.CopyFromLocal(source, rel);
        }
    }

    static void RemoveOwnedDiagnostics(SafDocumentTree tree)
    {
        const string baseGame = "moddingapi/mods/base_game";
        foreach (string name in OwnedDiagnosticDlls)
            tree.DeleteFile(baseGame + "/" + name);
    }

    static void VerifyConditionFix(SafDocumentTree tree)
    {
        const string rel = "moddingapi/mods/base_game/NSCApiConditionCompatFix_v2.dll";
        if (!tree.Exists(rel) || !tree.Sha256(rel).Equals(ModdingApiInstaller.ConditionCompatFixSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("SC 1.70 condition compatibility fix failed SAF-mode integrity verification after install.");
    }

    static int RestoreRemainingBackups(SafDocumentTree tree)
    {
        int restored = 0;
        foreach (string relRoot in new[] { "data/ui/flash/OTHER", "data/system", "moddingapi/mods/base_game" })
        {
            foreach (string backup in tree.FindFilesWithSuffix(relRoot, BackupSuffix).OrderByDescending(x => x.Length))
            {
                string target = backup[..^BackupSuffix.Length];
                tree.CopyRemote(backup, target);
                tree.DeleteFile(backup);
                restored++;
            }
        }
        return restored;
    }

    static int PruneKnownEmptyDirectories(SafDocumentTree tree)
    {
        int count = 0;
        foreach (string rel in new[] { "moddingapi/mods/base_game", "moddingapi/mods", "moddingapi/param/NSC", "moddingapi/param/NS4", "moddingapi/param", "moddingapi" })
        {
            try { if (tree.RemoveDirectoryIfEmpty(rel)) count++; }
            catch { /* Directory pruning is cosmetic; never fail cleanup because a provider refuses it. */ }
        }
        return count;
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
}
