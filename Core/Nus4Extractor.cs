using System.IO.Compression;

namespace NSC_ModManager_Android.Core;

/// <summary>
/// Portable Phase-1 converter for .nus4 packages.
/// This intentionally contains no references to the original Windows UI/view-model layer.
/// PRM/message-XFBIN transforms are handled by the semantic compiler when their feature handlers are available.
/// </summary>
public static class Nus4Extractor
{
    private static readonly string[] ParamKeywords =
    {
        "characode", "damageprm", "duelPlayerParam", "playerSettingParam", "skillCustomizeParam",
        "spSkillCustomizeParam", "characterSelectParam", "afterAttachObject", "costumeParam",
        "playerDoubleEffectParam", "cmnparam", "supportActionParam", "player_icon", "awakeAura",
        "appearanceAnm", "skillIndexSettingParam", "spTypeSupportParam", "privateCamera",
        "costumeBreakParam", "costumeBreakColorParam", "supportSkillRecoverySpeedParam",
        "damageeff", "effectprm", "StageInfo", "stageInfo", "messageInfo", "commandListParam",
        "Dictionary", "finalSpSkillCutIn", "flagprm", "hugeAwakeComboCameraParam",
        "meDecalParam", "situationVoice", "playerDecalSetting", "pairSpSkillCombinationParam"
    };

    public static void Extract(string nus4Path, string destinationFolder)
    {
        if (!File.Exists(nus4Path))
            throw new FileNotFoundException("NUS4 file not found.", nus4Path);

        byte[] fileData = File.ReadAllBytes(nus4Path);
        int zipOffset = FindZipOffset(fileData);
        if (zipOffset < 0)
            throw new InvalidDataException("Cannot extract .nus4: embedded ZIP archive not found.");

        string tempRoot = Path.Combine(Path.GetTempPath(), "nus4_" + Guid.NewGuid().ToString("N"));
        string tempZip = Path.Combine(tempRoot, "payload.zip");
        string extractedTemp = Path.Combine(tempRoot, "extracted");
        Directory.CreateDirectory(extractedTemp);

        var compatibilityNotes = new List<string>();

        try
        {
            using (var output = File.Create(tempZip))
                output.Write(fileData, zipOffset, fileData.Length - zipOffset);

            ZipFile.ExtractToDirectory(tempZip, extractedTemp, overwriteFiles: true);

            Directory.CreateDirectory(destinationFolder);
            string charactersRoot = Path.Combine(destinationFolder, "Characters");
            string stagesRoot = Path.Combine(destinationFolder, "Stages");
            string resourcesRoot = Path.Combine(destinationFolder, "Resources");
            string resourcesFilesData = Path.Combine(resourcesRoot, "Files", "data");
            string resourcesCpk = Path.Combine(resourcesRoot, "CPKs");
            string resourcesShaders = Path.Combine(resourcesRoot, "Shaders");

            Directory.CreateDirectory(charactersRoot);
            Directory.CreateDirectory(stagesRoot);
            Directory.CreateDirectory(resourcesFilesData);
            Directory.CreateDirectory(resourcesCpk);
            Directory.CreateDirectory(resourcesShaders);

            WriteModMetadata(nus4Path, extractedTemp, destinationFolder);
            CopyCpks(extractedTemp, resourcesCpk);

            foreach (string characodePath in Directory.GetFiles(extractedTemp, "characode.txt", SearchOption.AllDirectories))
            {
                string? characterFolder = Path.GetDirectoryName(characodePath);
                if (string.IsNullOrEmpty(characterFolder))
                    continue;

                string characterName = new DirectoryInfo(characterFolder).Name;
                string characterDestination = Path.Combine(charactersRoot, characterName);
                Directory.CreateDirectory(characterDestination);

                var characterIni = new IniFile(Path.Combine(characterDestination, "character_config.ini"));
                bool partner = Directory.Exists(Path.Combine(characterFolder, "moddingapi")) &&
                               Directory.GetFiles(Path.Combine(characterFolder, "moddingapi"), "partnerSlotParam.xfbin", SearchOption.AllDirectories).Length > 0;
                characterIni.Write("Partner", partner ? "true" : "false", "ModManager");
                characterIni.Write("Page", "-1", "ModManager");
                characterIni.Write("Slot", "-1", "ModManager");
                characterIni.Write("Game", "NS4", "ModManager");
                characterIni.Write("Page_NS4", "-1", "ModManager");
                characterIni.Write("Slot_NS4", "-1", "ModManager");

                string moddingApiSource = Path.Combine(characterFolder, "moddingapi");
                if (Directory.Exists(moddingApiSource))
                {
                    string moddingApiDestination = Path.Combine(characterDestination, "moddingapi", "mods", "base_game");
                    Directory.CreateDirectory(moddingApiDestination);
                    foreach (string xfbin in Directory.GetFiles(moddingApiSource, "*.xfbin", SearchOption.AllDirectories))
                        File.Copy(xfbin, Path.Combine(moddingApiDestination, Path.GetFileName(xfbin)), overwrite: true);
                }

                string dataWin32 = Path.Combine(characterFolder, "data_win32");
                if (Directory.Exists(dataWin32))
                {
                    string characterData = Path.Combine(characterDestination, "data");
                    CopyDataWin32(dataWin32, characterData, resourcesFilesData);

                    if (Directory.GetFiles(dataWin32, "*prm.bin.xfbin", SearchOption.AllDirectories).Length > 0)
                        compatibilityNotes.Add($"Character '{characterName}': PRM compatibility transform is deferred to the semantic compiler.");
                }

                string shadersSource = Path.Combine(characterFolder, "shaders");
                if (Directory.Exists(shadersSource))
                    CopyAllPreserveStructure(shadersSource, resourcesShaders);
            }

            foreach (string stageMessagePath in Directory.GetFiles(extractedTemp, "stageMessage.txt", SearchOption.AllDirectories))
            {
                string? stageFolder = Path.GetDirectoryName(stageMessagePath);
                if (string.IsNullOrEmpty(stageFolder))
                    continue;

                string stageName = new DirectoryInfo(stageFolder).Name;
                string stageDestination = Path.Combine(stagesRoot, stageName);
                Directory.CreateDirectory(stageDestination);

                string bgmIdPath = Path.Combine(stageFolder, "BGM_ID.txt");
                string bgmId = File.Exists(bgmIdPath) ? File.ReadAllText(bgmIdPath).Trim() : string.Empty;
                string stageMessageId = stageName + "_stageName";

                var stageIni = new IniFile(Path.Combine(stageDestination, "stage_config.ini"));
                stageIni.Write("BGM_ID", bgmId, "ModManager");
                stageIni.Write("BGM_ID_NS4", bgmId, "ModManager");
                stageIni.Write("MessageID", stageMessageId, "ModManager");
                stageIni.Write("Hell", "false", "ModManager");
                stageIni.Write("Game", "NS4", "ModManager");

                // Keep the source text so the full Android compiler can generate messageInfo later.
                File.Copy(stageMessagePath, Path.Combine(stageDestination, "stageMessage.txt"), overwrite: true);
                if (File.Exists(bgmIdPath))
                    File.Copy(bgmIdPath, Path.Combine(stageDestination, "BGM_ID.txt"), overwrite: true);

                string dataWin32 = Path.Combine(stageFolder, "data_win32");
                if (Directory.Exists(dataWin32))
                    CopyDataWin32(dataWin32, Path.Combine(stageDestination, "data"), resourcesFilesData);

                string stagePreview = Path.Combine(stageFolder, "stage_tex.png");
                if (File.Exists(stagePreview))
                    File.Copy(stagePreview, Path.Combine(stageDestination, "stage_preview.png"), overwrite: true);

                compatibilityNotes.Add($"Stage '{stageName}': stage message XFBIN generation is deferred to the semantic compiler; stageMessage.txt was preserved.");
            }

            if (compatibilityNotes.Count > 0)
            {
                string notesPath = Path.Combine(destinationFolder, "android_port_notes.txt");
                File.WriteAllLines(notesPath, compatibilityNotes.Distinct(StringComparer.Ordinal));
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }
    }

    private static int FindZipOffset(byte[] data)
    {
        ReadOnlySpan<byte> signature = stackalloc byte[] { 0x50, 0x4B, 0x03, 0x04 };
        for (int i = 0; i <= data.Length - signature.Length; i++)
        {
            if (data.AsSpan(i, signature.Length).SequenceEqual(signature))
                return i;
        }
        return -1;
    }

    private static void WriteModMetadata(string nus4Path, string extractedRoot, string destinationFolder)
    {
        string? icon = Directory.GetFiles(extractedRoot, "Icon.png", SearchOption.AllDirectories).FirstOrDefault();
        if (icon is not null)
            File.Copy(icon, Path.Combine(destinationFolder, "mod_icon.png"), overwrite: true);

        string? descriptionFile = Directory.GetFiles(extractedRoot, "Description.txt", SearchOption.AllDirectories).FirstOrDefault();
        string? authorFile = Directory.GetFiles(extractedRoot, "Author.txt", SearchOption.AllDirectories).FirstOrDefault();
        string description = descriptionFile is null ? string.Empty : File.ReadAllText(descriptionFile).Trim();
        string author = authorFile is null ? string.Empty : File.ReadAllText(authorFile).Trim();

        var ini = new IniFile(Path.Combine(destinationFolder, "mod_config.ini"));
        ini.Write("ModName", Path.GetFileNameWithoutExtension(nus4Path), "ModManager");
        ini.Write("Description", description, "ModManager");
        ini.Write("Author", author, "ModManager");
        ini.Write("LastUpdate", DateTime.Today.ToString("dd/MM/yyyy"), "ModManager");
        ini.Write("Version", "1.0", "ModManager");
        ini.Write("Game", "NS4", "ModManager");
        ini.Write("EnableMod", "true", "ModManager");
    }

    private static void CopyCpks(string extractedRoot, string destination)
    {
        foreach (string cpk in Directory.GetFiles(extractedRoot, "*.cpk", SearchOption.AllDirectories))
            File.Copy(cpk, Path.Combine(destination, Path.GetFileName(cpk)), overwrite: true);
    }

    private static void CopyDataWin32(string source, string parameterDestination, string resourceDestination)
    {
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string extension = Path.GetExtension(file);
            bool supported = extension.Equals(".xfbin", StringComparison.OrdinalIgnoreCase) ||
                             extension.Equals(".acb", StringComparison.OrdinalIgnoreCase) ||
                             extension.Equals(".awb", StringComparison.OrdinalIgnoreCase);
            if (!supported)
                continue;

            bool isXfbin = extension.Equals(".xfbin", StringComparison.OrdinalIgnoreCase);
            bool isParameter = isXfbin && ParamKeywords.Any(keyword => file.Contains(keyword, StringComparison.Ordinal));

            if (!isXfbin || isParameter)
            {
                string relative = Path.GetRelativePath(source, file);
                if (!relative.Contains("message", StringComparison.Ordinal))
                    relative = RemovePathSegment(relative, "WIN64");
                CopyFile(file, Path.Combine(parameterDestination, relative));
            }

            if (!isParameter)
            {
                string relative = Path.GetRelativePath(source, file);
                CopyFile(file, Path.Combine(resourceDestination, relative));
            }
        }
    }

    private static string RemovePathSegment(string relativePath, string segment)
    {
        string[] parts = relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        string[] filtered = parts.Where(p => !p.Equals(segment, StringComparison.OrdinalIgnoreCase)).ToArray();
        return filtered.Length == 0 ? Path.GetFileName(relativePath) : Path.Combine(filtered);
    }

    private static void CopyAllPreserveStructure(string sourceDir, string targetDir)
    {
        foreach (string sourceFile in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDir, sourceFile);
            CopyFile(sourceFile, Path.Combine(targetDir, relative));
        }
    }

    private static void CopyFile(string source, string destination)
    {
        string? directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.Copy(source, destination, overwrite: true);
    }
}
