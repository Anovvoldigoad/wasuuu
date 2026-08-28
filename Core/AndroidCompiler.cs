using System.Text;

namespace NSC_ModManager_Android.Core;

public sealed class AndroidCompiler
{
    private readonly ModRepository _repo = new();

    public CompileResult Compile(
        string gamePath,
        string modsPath,
        string moddingApiPayloadZip,
        string workRoot,
        Action<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new CompileResult();
        progress ??= _ => { };

        var pathCheck = PathValidator.ValidateGamePath(gamePath);
        if (!pathCheck.Ok) throw new DirectoryNotFoundException(pathCheck.Message);
        if (!Directory.Exists(modsPath)) throw new DirectoryNotFoundException("Mod storage directory does not exist.");

        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<ModInfo> enabled = _repo.Scan(modsPath)
            .Where(m => m.Enabled)
            .OrderBy(m => m.RootPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        result.EnabledMods = enabled.Count;
        if (enabled.Count == 0)
            throw new InvalidOperationException("No enabled mods found.");

        if (Directory.Exists(workRoot)) Directory.Delete(workRoot, true);
        Directory.CreateDirectory(workRoot);
        string cpkAssets = Path.Combine(workRoot, "cpk_assets");
        string dataWin32 = Path.Combine(workRoot, "data_win32_modmanager");
        Directory.CreateDirectory(cpkAssets);
        Directory.CreateDirectory(dataWin32);

        int cpkIndex = 0;
        foreach (ModInfo mod in enabled)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress($"Reading {mod.Name}...");

            string resources = Path.Combine(mod.RootPath, "Resources", "Files");
            result.ResourceFiles += FileTree.CopyResourceFiles(resources, dataWin32, result);

            // Old .nus4 conversion and native NSC packages may place parameter-like data under
            // Characters/Stages. Count and validate them now, but do not perform an unsafe whole-file
            // override. Phase 2B will merge their records into vanilla parameter XFBINs.
            ScanPendingParameterFiles(mod, result);
            result.CharacterConfigsDetected += Directory.EnumerateFiles(mod.RootPath, "character_config.ini", SearchOption.AllDirectories).Count();
            result.StageConfigsDetected += Directory.EnumerateFiles(mod.RootPath, "stage_config.ini", SearchOption.AllDirectories).Count();
            result.ModelConfigsDetected += Directory.EnumerateFiles(mod.RootPath, "model_config.ini", SearchOption.AllDirectories).Count();

            foreach (string cpk in Directory.EnumerateFiles(mod.RootPath, "*.cpk", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.CpkArchivesRead++;
                string extractDir = Path.Combine(workRoot, "extract", $"{cpkIndex++:D4}");
                Directory.CreateDirectory(extractDir);
                progress($"Extracting {Path.GetFileName(cpk)}...");
                int code = NativeCpk.Extract(cpk, extractDir);
                if (code != 0)
                {
                    result.Warnings.Add($"CPK extract failed ({code}): {cpk}");
                    continue;
                }
                FileTree.CopyAll(extractDir, cpkAssets, overwrite: true);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        progress("Preparing shader merge...");
        string generated = Path.Combine(workRoot, "generated");
        Directory.CreateDirectory(generated);
        string stagedShader = Path.Combine(generated, "nuccMaterial_dx11.nsh");
        result.ShaderFiles = ShaderMerger.BuildMergedFile(gamePath, enabled, stagedShader, result);

        // Build every archive in app cache first. Nothing generated is copied into the game
        // until all native pack operations have succeeded.
        if (FileTree.HasFiles(cpkAssets))
        {
            progress("Packing cpk_assets.cpk (ARM64 native)...");
            string output = Path.Combine(generated, "cpk_assets.cpk");
            PackChecked(cpkAssets, output);
            WriteInfo(output + ".info", 0x20);
            result.CpkArchivesPacked++;
        }

        if (FileTree.HasFiles(dataWin32))
        {
            progress("Packing data_win32_modmanager.cpk (ARM64 native)...");
            string output = Path.Combine(generated, "data_win32_modmanager.cpk");
            PackChecked(dataWin32, output);
            WriteInfo(output + ".info", 0x21);
            result.CpkArchivesPacked++;
        }

        cancellationToken.ThrowIfCancellationRequested();
        progress("Installing/updating ModdingAPI payload...");
        result.ModdingApiFilesInstalled = ModdingApiInstaller.Install(moddingApiPayloadZip, gamePath);
        progress("Installing generated files into game directory...");
        string baseGame = Path.Combine(gamePath, "moddingapi", "mods", "base_game");
        Directory.CreateDirectory(baseGame);
        DeleteGenerated(baseGame);
        foreach (string file in Directory.EnumerateFiles(generated, "*.cpk*", SearchOption.TopDirectoryOnly))
            File.Copy(file, Path.Combine(baseGame, Path.GetFileName(file)), true);
        ShaderMerger.InstallMergedFile(gamePath, stagedShader);

        if (result.ParameterXfbinsDetected > 0 || result.CharacterConfigsDetected > 0 || result.StageConfigsDetected > 0 || result.ModelConfigsDetected > 0)
        {
            result.Warnings.Add(
                "Semantic parameter merge is intentionally deferred to Phase 2B. " +
                "Parameter/roster/stage files were detected but were NOT installed as whole-file overrides. " +
                "Any pre-existing param_files.cpk was left untouched for safety.");
        }

        result.ReportPath = WriteReport(gamePath, result, enabled);
        progress("Compile finished.");
        return result;
    }

    private static void ScanPendingParameterFiles(ModInfo mod, CompileResult result)
    {
        foreach (string xfbin in Directory.EnumerateFiles(mod.RootPath, "*.xfbin", SearchOption.AllDirectories))
        {
            // Files directly under Resources/Files are counted by CopyResourceFiles already.
            string resourcesRoot = Path.Combine(mod.RootPath, "Resources", "Files") + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(xfbin);
            if (full.StartsWith(Path.GetFullPath(resourcesRoot), StringComparison.Ordinal)) continue;
            if (!XfbinPreflight.IsParameterXfbin(xfbin)) continue;

            result.ParameterXfbinsDetected++;
            if (!XfbinPreflight.TryReadHeader(xfbin, out _, out string error))
                result.Warnings.Add($"Invalid pending XFBIN '{Path.GetFileName(xfbin)}' in {mod.Name}: {error}");
        }
    }

    private static void PackChecked(string input, string output)
    {
        if (File.Exists(output)) File.Delete(output);
        int code = NativeCpk.Pack(input, output, compress: false, mode: 1);
        if (code != 0 || !File.Exists(output))
            throw new InvalidOperationException($"Native CPK pack failed with exit code {code}: {Path.GetFileName(output)}");
    }

    private static void DeleteGenerated(string baseGame)
    {
        // Phase 2A only owns these two generated archives. Keep any pre-existing
        // resources_modmanager.cpk / param_files.cpk untouched until Phase 2B can
        // regenerate them safely.
        string[] names =
        {
            "cpk_assets.cpk", "cpk_assets.cpk.info",
            "data_win32_modmanager.cpk", "data_win32_modmanager.cpk.info"
        };
        foreach (string name in names)
        {
            string path = Path.Combine(baseGame, name);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void WriteInfo(string path, byte id)
        => File.WriteAllBytes(path, new byte[] { id, 0, 0, 0, 1, 0, 0, 0 });

    private static string WriteReport(string gamePath, CompileResult result, IReadOnlyList<ModInfo> mods)
    {
        string reportDir = Path.Combine(gamePath, "moddingapi", "mods", "base_game");
        Directory.CreateDirectory(reportDir);
        string path = Path.Combine(reportDir, "nsc_android_compile_report.txt");
        var sb = new StringBuilder();
        sb.AppendLine("NSC Mod Manager Android — Phase 2 compile report");
        sb.AppendLine($"Time: {DateTime.Now:O}");
        sb.AppendLine($"Game: {gamePath}");
        sb.AppendLine($"Enabled mods: {result.EnabledMods}");
        foreach (ModInfo mod in mods) sb.AppendLine($"  - {mod.Name} ({mod.RootPath})");
        sb.AppendLine($"Resource files staged: {result.ResourceFiles}");
        sb.AppendLine($"CPKs read: {result.CpkArchivesRead}");
        sb.AppendLine($"CPKs generated: {result.CpkArchivesPacked}");
        sb.AppendLine($"Shaders merged: {result.ShaderFiles}");
        sb.AppendLine($"Parameter XFBINs pending: {result.ParameterXfbinsDetected}");
        sb.AppendLine($"Character configs pending: {result.CharacterConfigsDetected}");
        sb.AppendLine($"Stage configs pending: {result.StageConfigsDetected}");
        sb.AppendLine($"Model configs pending: {result.ModelConfigsDetected}");
        sb.AppendLine();
        sb.AppendLine("Warnings:");
        if (result.Warnings.Count == 0) sb.AppendLine("  (none)");
        else foreach (string warning in result.Warnings.Distinct()) sb.AppendLine("  - " + warning);
        File.WriteAllText(path, sb.ToString());
        return path;
    }
}
