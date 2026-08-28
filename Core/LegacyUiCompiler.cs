using System.Text;
using NSC_ModManager.Model;
using NSC_ModManager.ViewModel;
using BinaryReader = NSC_ModManager.BinaryReader;

namespace NSC_ModManager_Android.Core;

/// <summary>
/// Portable version of the character/stage selection UI updates performed by
/// NSC ModManager 2.1.1.0.  Everything is generated into the temporary work
/// tree first; AndroidCompiler installs the overlay only after all CPK packs
/// have succeeded.
/// </summary>
internal static class LegacyUiCompiler
{
    public sealed record Output(
        string GameOverlayDirectory,
        int CharacterUiFiles,
        int StageUiFiles,
        int StageResourceXfbins,
        int FixedOverlayFiles,
        int BaseResourceFiles);

    public static Output Build(
        string baseNsc,
        string apiParam,
        string rootFolder,
        string paramDirectory,
        string generatedApiParam,
        CharacterSelectParamViewModel characterSelect,
        List<string> charselIconNames,
        IReadOnlyList<StageModModel> stagesToAdd,
        bool stageInfoModified,
        CompileResult result)
    {
        string overlay = Path.Combine(rootFolder, "game_overlay");
        Directory.CreateDirectory(overlay);
        string cpkAssets = Path.Combine(rootFolder, "cpk_assets");
        string dataWin32 = Path.Combine(rootFolder, "data_win32_modmanager");

        int characterUi = 0;
        int stageUi = 0;
        int stageResources = 0;
        int fixedOverlayFiles = StageFixedRuntime(baseNsc, overlay, rootFolder, result);
        int baseResourceFiles = StageBaseResources(baseNsc, rootFolder);

        // The original compiler always uses its known-good NSC char-selection
        // GFX as the base, then updates only the page count and dynamic icon list.
        string charselBase = Path.Combine(baseNsc, "charsel.gfx");
        if (File.Exists(charselBase))
        {
            byte[] charsel = File.ReadAllBytes(charselBase);
            const int pageCountOffset = 0x40950;
            if (charsel.Length <= pageCountOffset)
                throw new InvalidDataException("Bundled charsel.gfx is shorter than the expected NSC layout.");
            charsel[pageCountOffset] = checked((byte)(1 + characterSelect.MaxPage()));
            WriteOverlay(overlay, Path.Combine("data", "ui", "flash", "OTHER", "charsel", "charsel.gfx"), charsel);
            characterUi++;
        }
        else
        {
            result.Warnings.Add("Character selection UI baseline (charsel.gfx) is missing; roster parameter merge can still complete, but extra pages may not be reachable.");
        }

        // Default icon(s) shipped with the desktop manager are treated exactly
        // like mod-provided icons and are packed into cpk_assets.
        string defaultIcons = Path.Combine(baseNsc, "DefaultIcons");
        if (Directory.Exists(defaultIcons))
        {
            foreach (string icon in Directory.EnumerateFiles(defaultIcons, "*.xfbin", SearchOption.AllDirectories)
                         .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                string dst = Path.Combine(cpkAssets, "data", "ui", "flash", "OTHER", "charicon_s", Path.GetFileName(icon));
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.Copy(icon, dst, true);
                string iconName = Path.GetFileNameWithoutExtension(icon).Replace("_charicon_s", "", StringComparison.OrdinalIgnoreCase);
                if (!charselIconNames.Contains(iconName, StringComparer.OrdinalIgnoreCase))
                    charselIconNames.Add(iconName);
            }
        }

        // Remove icon registrations for resources that do not actually exist in
        // either generated CPK input tree (or in the bundled defaults).
        for (int i = charselIconNames.Count - 1; i >= 0; i--)
        {
            string iconFile = charselIconNames[i] + "_charicon_s.xfbin";
            bool exists = File.Exists(Path.Combine(cpkAssets, "data", "ui", "flash", "OTHER", "charicon_s", iconFile))
                       || File.Exists(Path.Combine(dataWin32, "data", "ui", "flash", "OTHER", "charicon_s", iconFile))
                       || File.Exists(Path.Combine(defaultIcons, iconFile));
            if (!exists) charselIconNames.RemoveAt(i);
        }

        string chariconBase = Path.Combine(baseNsc, "charicon_s.gfx");
        if (File.Exists(chariconBase))
        {
            byte[] patched = BuildCharacterIconGfx(File.ReadAllBytes(chariconBase), charselIconNames);
            WriteOverlay(overlay, Path.Combine("data", "ui", "flash", "OTHER", "charicon_s", "charicon_s.gfx"), patched);
            characterUi++;
        }
        else if (charselIconNames.Count > 0)
        {
            result.Warnings.Add($"{charselIconNames.Count} character icon registration(s) are needed but bundled charicon_s.gfx is missing.");
        }

        if (stageInfoModified && stagesToAdd.Count > 0)
        {
            string selectStageBase = Path.Combine(baseNsc, "select_stage.xfbin");
            string stageselImageBase = Path.Combine(baseNsc, "stagesel_image.gfx");
            string stageselBase = Path.Combine(baseNsc, "stagesel.gfx");
            string defaultStageTex = Path.Combine(baseNsc, "Templates", "stage_tex.png");
            string defaultStageIcon = Path.Combine(baseNsc, "Templates", "stage_icon.dds");
            string bgmBase = Path.Combine(apiParam, "bgmManagerParam.xfbin");

            string[] required = { selectStageBase, stageselImageBase, stageselBase, defaultStageTex, defaultStageIcon, bgmBase };
            string? missing = required.FirstOrDefault(p => !File.Exists(p));
            if (missing is not null)
                throw new FileNotFoundException("Bundled stage UI baseline is incomplete.", missing);

            byte[] bgm = File.ReadAllBytes(bgmBase);
            BuildStageSelection(
                stagesToAdd,
                File.ReadAllBytes(selectStageBase),
                File.ReadAllBytes(stageselImageBase),
                File.ReadAllBytes(stageselBase),
                defaultStageTex,
                defaultStageIcon,
                cpkAssets,
                paramDirectory,
                overlay,
                ref bgm,
                ref stageUi,
                ref stageResources);

            Directory.CreateDirectory(generatedApiParam);
            File.WriteAllBytes(Path.Combine(generatedApiParam, "bgmManagerParam.xfbin"), bgm);
        }

        return new Output(overlay, characterUi, stageUi, stageResources, fixedOverlayFiles, baseResourceFiles);
    }

    private static int StageFixedRuntime(string baseNsc, string overlay, string rootFolder, CompileResult result)
    {
        string runtime = Path.Combine(baseNsc, "Runtime");
        if (!Directory.Exists(runtime))
        {
            result.Warnings.Add("Bundled NSC runtime overlay is missing; semantic parameters can still be built but desktop-equivalent UI/system patches were skipped.");
            return 0;
        }

        (string Source, string Relative)[] files =
        {
            ("gametitle.gfx", Path.Combine("data", "ui", "flash", "OTHER", "gametitle", "gametitle.gfx")),
            ("xcmn_win_roll1.gfx", Path.Combine("data", "ui", "flash", "OTHER", "gametitle", "xcmn_win_roll1.gfx")),
            ("celshade.tex.xfbin", Path.Combine("data", "system", "celshade.tex.xfbin")),
            ("patchnotes.txt", Path.Combine("data", "ui", "flash", "OTHER", "gametitle", "patchnotes.txt")),
            ("gauge_p.gfx", Path.Combine("data", "ui", "flash", "OTHER", "gauge_p", "gauge_p.gfx")),
            ("freebtl_set.gfx", Path.Combine("data", "ui", "flash", "OTHER", "freebtl_set", "freebtl_set.gfx")),
        };

        int count = 0;
        foreach (var item in files)
        {
            string src = Path.Combine(runtime, item.Source);
            if (!File.Exists(src))
            {
                result.Warnings.Add("Bundled runtime file missing: " + item.Source);
                continue;
            }
            string dst = Path.Combine(overlay, item.Relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            File.Copy(src, dst, true);
            count++;
        }
        return count;
    }

    private static int StageBaseResources(string baseNsc, string rootFolder)
    {
        string source = Path.Combine(baseNsc, "Resources");
        if (!Directory.Exists(source)) return 0;
        string destination = Path.Combine(rootFolder, "resources_modmanager");
        Directory.CreateDirectory(destination);
        int count = 0;
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(source, file);
            string dst = Path.Combine(destination, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            File.Copy(file, dst, true);
            count++;
        }
        return count;
    }

    private static byte[] BuildCharacterIconGfx(byte[] fileBytes, IReadOnlyList<string> iconNames)
    {
        // Offsets/constants are from NSC ModManager 2.1.1.0's NSC compiler and
        // intentionally target its bundled NSC baseline, not an arbitrary game GFX.
        if (fileBytes.Length < 0x1ABB4)
            throw new InvalidDataException("Bundled charicon_s.gfx is shorter than the expected NSC layout.");

        byte[] header = BinaryReader.b_ReadByteArray(fileBytes, 0, 0xCC);
        byte[] body1 = BinaryReader.b_ReadByteArray(fileBytes, 0xCC, 0x460E);
        byte[] body2 = BinaryReader.b_ReadByteArray(fileBytes, 0x46DA, 0x1232);
        byte[] end = BinaryReader.b_ReadByteArray(fileBytes, 0x590C, 0x152A8);
        byte[] output = Array.Empty<byte>();
        const int iconCount = 0x1D2;
        const int iconCount2 = 0xE5;
        const int externalImageCount = 5;

        for (int i = 0; i < iconNames.Count; i++)
        {
            string iconName = iconNames[i];
            string ddsName = iconName + "_charicon_s.dds";
            byte[] extra = Array.Empty<byte>();
            extra = BinaryReader.b_AddBytes(extra, BitConverter.GetBytes(0x4C + ddsName.Length), 0, 0, 1);
            extra = BinaryReader.b_AddBytes(extra, new byte[] { 0xFC });
            extra = BinaryReader.b_AddBytes(extra, BitConverter.GetBytes(externalImageCount + i), 0, 0, 2);
            extra = BinaryReader.b_AddBytes(extra, new byte[] { 0x09, 0x00, 0x0E, 0x00, 0x80, 0x00, 0x80, 0x00, 0x00 });
            extra = BinaryReader.b_AddBytes(extra, BitConverter.GetBytes(ddsName.Length), 0, 0, 1);
            extra = BinaryReader.b_AddBytes(extra, Encoding.ASCII.GetBytes(ddsName));
            header = BinaryReader.b_AddBytes(header, extra);

            byte[] section = new byte[0x47]
            {
                0x0C,0xFC,0x85,0x01,0x00,0x00,0x00,0x00,0x00,0x00,0x80,0x00,0x80,0x00,0xBF,0x00,
                0x33,0x00,0x00,0x00,0x86,0x01,0x65,0x80,0x28,0x05,0x80,0x28,0x00,0x02,0x41,0xFF,
                0xFF,0xD9,0x40,0x00,0x05,0x00,0x00,0x00,0x41,0x85,0x01,0xD9,0x40,0x00,0x05,0x00,
                0x00,0x0C,0xB0,0x0B,0x00,0x00,0x20,0x15,0x96,0x01,0x60,0x17,0x62,0x80,0x3B,0x54,
                0x01,0xD9,0x60,0x0E,0xDB,0x00,0x00
            };
            section = BinaryReader.b_ReplaceBytes(section, BitConverter.GetBytes((short)(iconCount + i * 2)), 0x02, 0, 2);
            section = BinaryReader.b_ReplaceBytes(section, BitConverter.GetBytes((short)(iconCount + 1 + i * 2)), 0x14, 0, 2);
            section = BinaryReader.b_ReplaceBytes(section, BitConverter.GetBytes((short)(iconCount + i * 2)), 0x29, 0, 2);
            section = BinaryReader.b_ReplaceBytes(section, BitConverter.GetBytes((short)(externalImageCount + i)), 0x04, 0, 2);
            body1 = BinaryReader.b_AddBytes(body1, section);

            byte[] name = Array.Empty<byte>();
            name = BinaryReader.b_AddBytes(name, new byte[] { 0xFF, 0x0A });
            name = BinaryReader.b_AddBytes(name, BitConverter.GetBytes(iconName.Length + 1));
            name = BinaryReader.b_AddBytes(name, Encoding.ASCII.GetBytes(iconName));
            name = BinaryReader.b_AddBytes(name, new byte[] { 0x00, 0x85, 0x06, 0x03, 0x01, 0x00 });
            name = BinaryReader.b_AddBytes(name, BitConverter.GetBytes((short)(iconCount + 1 + i * 2)), 0, 0);
            name = BinaryReader.b_AddBytes(name, new byte[] { 0x40, 0x00 });
            body2 = BinaryReader.b_AddBytes(body2, name);
        }

        body2 = BinaryReader.b_ReplaceBytes(body2, BitConverter.GetBytes(body2.Length - 4), 0x02, 0);
        body2 = BinaryReader.b_ReplaceBytes(body2, BitConverter.GetBytes(iconCount + iconNames.Count * 2), 0x06, 0, 2);
        body2 = BinaryReader.b_ReplaceBytes(body2, BitConverter.GetBytes(iconCount2 + iconNames.Count), 0x08, 0, 2);
        end = BinaryReader.b_ReplaceBytes(end, BitConverter.GetBytes(iconCount + 1 + iconNames.Count * 2), 0x08, 0, 2);
        end = BinaryReader.b_ReplaceBytes(end, BitConverter.GetBytes(iconCount + iconNames.Count * 2), 0x15, 0, 2);
        end = BinaryReader.b_ReplaceBytes(end, BitConverter.GetBytes(iconCount + 1 + iconNames.Count * 2), 0x7D, 0, 2);
        end = BinaryReader.b_ReplaceBytes(end, BitConverter.GetBytes(iconCount + iconNames.Count * 2), 0x151FA, 0, 2);
        output = BinaryReader.b_AddBytes(output, header);
        output = BinaryReader.b_AddBytes(output, body1);
        output = BinaryReader.b_AddBytes(output, body2);
        output = BinaryReader.b_AddBytes(output, end);
        output = BinaryReader.b_ReplaceBytes(output, BitConverter.GetBytes(output.Length), 0x04, 0);
        return output;
    }

    private static void BuildStageSelection(
        IReadOnlyList<StageModModel> stages,
        byte[] stageSelect,
        byte[] stageselImageOriginal,
        byte[] stageselOriginal,
        string defaultStageTex,
        string defaultStageIcon,
        string cpkAssets,
        string paramDirectory,
        string overlay,
        ref byte[] bgm,
        ref int stageUi,
        ref int stageResources)
    {
        const int stageCount = 67;
        if (stageSelect.Length < 0x13F2 || stageselImageOriginal.Length < 0x2661 || stageselOriginal.Length <= 0x29E22)
            throw new InvalidDataException("Bundled stage-selection UI files do not match the expected NSC layout.");

        byte[] header = BinaryReader.b_ReadByteArray(stageSelect, 0, 0x13C);
        byte[] body = BinaryReader.b_ReadByteArray(stageSelect, 0x13C, 0x1298);
        byte[] end = BinaryReader.b_ReadByteArray(stageSelect, 0x13DE, 0x14);
        byte[] xmlAdd = Array.Empty<byte>();

        for (int st = 0; st < stages.Count; st++)
        {
            StageModModel stage = stages[st];
            byte[] bgmEntry = new byte[0x68];
            bgmEntry = BinaryReader.b_ReplaceString(bgmEntry, stage.StageName, 0);
            bgmEntry = BinaryReader.b_ReplaceBytes(bgmEntry, BitConverter.GetBytes(stage.BgmID), 0x60);
            bgmEntry = BinaryReader.b_ReplaceBytes(bgmEntry, BitConverter.GetBytes(-1), 0x64);
            bgm = BinaryReader.b_AddBytes(bgm, bgmEntry);

            byte[] xml = new byte[] { 0x0D,0x0A,0x09,0x3C,0x73,0x74,0x61,0x67,0x65,0x20,0x69,0x64,0x3D,0x22 };
            xml = BinaryReader.b_AddBytes(xml, Encoding.ASCII.GetBytes((stageCount + st).ToString()));
            xml = BinaryReader.b_AddBytes(xml, new byte[] { 0x22,0x20,0x6E,0x61,0x6D,0x65,0x69,0x64,0x3D,0x22 });
            xml = BinaryReader.b_AddBytes(xml, Encoding.ASCII.GetBytes(stage.MessageID ?? string.Empty));
            xml = BinaryReader.b_AddBytes(xml, new byte[] { 0x22,0x20,0x73,0x74,0x61,0x67,0x65,0x69,0x64,0x3D,0x22 });
            xml = BinaryReader.b_AddBytes(xml, Encoding.ASCII.GetBytes(stage.StageName));
            xml = BinaryReader.b_AddBytes(xml, new byte[] { 0x22,0x20,0x68,0x65,0x6C,0x6C,0x3D,0x22 });
            xml = BinaryReader.b_AddBytes(xml, Encoding.ASCII.GetBytes(stage.Hell ? "1" : "0"));
            xml = BinaryReader.b_AddBytes(xml, new byte[] { 0x22,0x2F,0x3E });
            xmlAdd = BinaryReader.b_AddBytes(xmlAdd, xml);

            string preview = Path.Combine(stage.RootPath, "stage_preview.png");
            byte[] previewBytes = File.ReadAllBytes(File.Exists(preview) ? preview : defaultStageTex);
            byte[] previewXfbin = BinaryReader.MakeXfbinBinary(
                $"Z:/char/x/stagesel/tex/tex_l/st_img_l_{stageCount - 1 + st}.png",
                $"st_img_l_{stageCount - 1 + st}", previewBytes);
            string previewOut = Path.Combine(cpkAssets, "data", "ui", "flash", "OTHER", "stagesel", "tex_l", $"st_img_l_{stageCount - 1 + st}.xfbin");
            Directory.CreateDirectory(Path.GetDirectoryName(previewOut)!);
            File.WriteAllBytes(previewOut, previewXfbin);
            stageResources++;

            string icon = Path.Combine(stage.RootPath, "stage_icon.dds");
            string iconSc = Path.Combine(stage.RootPath, "stage_icon_SC.dds");
            byte[] iconBytes = File.ReadAllBytes(File.Exists(iconSc) ? iconSc : File.Exists(icon) ? icon : defaultStageIcon);
            byte[] iconXfbin = BinaryReader.MakeXfbinBinary(
                "D:/usr/flash/char/x/stagesel/" + stage.StageName + ".dds",
                "stagesel_image_" + stage.StageName, iconBytes);
            string iconOut = Path.Combine(cpkAssets, "data", "ui", "flash", "OTHER", "stagesel", "stagesel_image_" + stage.StageName + ".xfbin");
            Directory.CreateDirectory(Path.GetDirectoryName(iconOut)!);
            File.WriteAllBytes(iconOut, iconXfbin);
            stageResources++;
        }

        xmlAdd = BinaryReader.b_AddBytes(xmlAdd, new byte[] { 0x0D,0x0A,0x3C,0x2F,0x5F,0x72,0x6F,0x6F,0x74,0x3E });
        byte[] stageNew = Array.Empty<byte>();
        stageNew = BinaryReader.b_AddBytes(stageNew, header);
        stageNew = BinaryReader.b_ReplaceBytes(stageNew, BitConverter.GetBytes(body.Length + xmlAdd.Length), 0x138, 1);
        stageNew = BinaryReader.b_ReplaceBytes(stageNew, BitConverter.GetBytes(body.Length + xmlAdd.Length + 4), 0x12C, 1);
        stageNew = BinaryReader.b_AddBytes(stageNew, body);
        stageNew = BinaryReader.b_AddBytes(stageNew, xmlAdd);
        stageNew = BinaryReader.b_AddBytes(stageNew, end);
        string selectOut = Path.Combine(paramDirectory, "data", "ui", "max", "select", "select_stage.xfbin");
        Directory.CreateDirectory(Path.GetDirectoryName(selectOut)!);
        File.WriteAllBytes(selectOut, stageNew);

        byte[] imageHeader = BinaryReader.b_ReadByteArray(stageselImageOriginal, 0x00, 0x78);
        byte[] imageHeaderAdd = Array.Empty<byte>();
        byte[] imageBody1 = BinaryReader.b_ReadByteArray(stageselImageOriginal, 0x78, 0x126E);
        byte[] imageBody1Add = Array.Empty<byte>();
        byte[] imageBody2 = BinaryReader.b_ReadByteArray(stageselImageOriginal, 0x12E6, 0x6F0);
        byte[] imageBody2Add = Array.Empty<byte>();
        byte[] imageEnd = BinaryReader.b_ReadByteArray(stageselImageOriginal, 0x19D6, 0xC8B);
        const int imageCount = 2;
        const int imageCount1 = 0x83;

        for (int st = 0; st < stages.Count; st++)
        {
            string stageName = stages[st].StageName;
            string fileName = "stagesel_image_" + stageName + ".dds";
            imageHeaderAdd = BinaryReader.b_AddBytes(imageHeaderAdd, new byte[] { (byte)(0x4C + fileName.Length), 0xFC });
            imageHeaderAdd = BinaryReader.b_AddBytes(imageHeaderAdd, BitConverter.GetBytes(st + imageCount), 0, 0, 2);
            imageHeaderAdd = BinaryReader.b_AddBytes(imageHeaderAdd, new byte[] { 0x09,0x00,0x0E,0x00,0xB8,0x00,0x68,0x00,0x00 });
            imageHeaderAdd = BinaryReader.b_AddBytes(imageHeaderAdd, new byte[] { (byte)fileName.Length });
            imageHeaderAdd = BinaryReader.b_AddBytes(imageHeaderAdd, Encoding.ASCII.GetBytes(fileName));

            imageBody1Add = BinaryReader.b_AddBytes(imageBody1Add, new byte[] { 0x0C,0xFC });
            imageBody1Add = BinaryReader.b_AddBytes(imageBody1Add, BitConverter.GetBytes(imageCount1 + ((st + 1) * 2)), 0, 0, 2);
            imageBody1Add = BinaryReader.b_AddBytes(imageBody1Add, BitConverter.GetBytes(st + imageCount), 0, 0, 2);
            imageBody1Add = BinaryReader.b_AddBytes(imageBody1Add, new byte[] { 0x00,0x00,0x00,0x00,0xB8,0x00,0x68,0x00,0xBF,0x00,0x33,0x00,0x00,0x00 });
            imageBody1Add = BinaryReader.b_AddBytes(imageBody1Add, BitConverter.GetBytes((imageCount1 + 1) + ((st + 1) * 2)), 0, 0, 2);
            imageBody1Add = BinaryReader.b_AddBytes(imageBody1Add, new byte[] { 0x64,0x54,0x3A,0xC5,0xF8,0x20,0x80,0x02,0x41,0xFF,0xFF,0xD9,0x40,0x00,0x05,0x00,0x00,0x00,0x41 });
            imageBody1Add = BinaryReader.b_AddBytes(imageBody1Add, BitConverter.GetBytes(imageCount1 + ((st + 1) * 2)), 0, 0, 2);
            imageBody1Add = BinaryReader.b_AddBytes(imageBody1Add, new byte[] { 0xD9,0x40,0x00,0x05,0x00,0x00,0x0C,0x8A,0x8B,0xF0,0x00,0x20,0x15,0x91,0x51,0x7E,0x17,0x63,0xAC,0x3B,0x50,0x41,0xD9,0x15,0x0E,0xDB,0xF0,0x00 });

            string imgName = "img_s_" + (stageCount - 1 + st);
            imageBody2Add = BinaryReader.b_AddBytes(imageBody2Add, new byte[] { 0xFF,0x0A,(byte)(imgName.Length + 1),0x00,0x00,0x00,0x69,0x6D,0x67,0x5F,0x73,0x5F });
            imageBody2Add = BinaryReader.b_AddBytes(imageBody2Add, Encoding.ASCII.GetBytes((stageCount - 1 + st).ToString()));
            imageBody2Add = BinaryReader.b_AddBytes(imageBody2Add, new byte[] { 0x00,0x85,0x06,0x03,0x01,0x00,(byte)(imageCount1 + 1 + ((st + 1) * 2)),0x00,0x40,0x00 });
        }

        imageEnd = BinaryReader.b_ReplaceBytes(imageEnd, BitConverter.GetBytes(imageCount1 + 4 + stages.Count * 2), 0xC64, 0, 2);
        imageBody2 = BinaryReader.b_ReplaceBytes(imageBody2, BitConverter.GetBytes(imageCount1 + 2 + stages.Count * 2), 0x06, 0, 2);
        imageBody2 = BinaryReader.b_ReplaceBytes(imageBody2, BitConverter.GetBytes(imageCount1 + 3 + stages.Count * 2), 0x82, 0, 2);
        imageBody2 = BinaryReader.b_ReplaceBytes(imageBody2, BitConverter.GetBytes(imageCount1 + 2 + stages.Count * 2), 0x8B, 0, 2);
        imageBody2 = BinaryReader.b_ReplaceBytes(imageBody2, BitConverter.GetBytes(0x695 + imageBody2Add.Length), 0xB7, 0, 2);
        imageBody2 = BinaryReader.b_ReplaceBytes(imageBody2, BitConverter.GetBytes(imageCount1 + 4 + stages.Count * 2), 0xBB, 0, 2);
        imageBody2 = BinaryReader.b_ReplaceBytes(imageBody2, BitConverter.GetBytes(stageCount + stages.Count), 0xBD, 0, 2);

        byte[] imageNew = Array.Empty<byte>();
        imageNew = BinaryReader.b_AddBytes(imageNew, imageHeader);
        imageNew = BinaryReader.b_AddBytes(imageNew, imageHeaderAdd);
        imageNew = BinaryReader.b_AddBytes(imageNew, imageBody1);
        imageNew = BinaryReader.b_AddBytes(imageNew, imageBody1Add);
        imageNew = BinaryReader.b_AddBytes(imageNew, imageBody2);
        imageNew = BinaryReader.b_AddBytes(imageNew, imageBody2Add);
        imageNew = BinaryReader.b_AddBytes(imageNew, imageEnd);
        imageNew = BinaryReader.b_ReplaceBytes(imageNew, BitConverter.GetBytes(imageNew.Length), 0x04);
        WriteOverlay(overlay, Path.Combine("data", "ui", "flash", "OTHER", "stagesel", "stagesel_image.gfx"), imageNew);
        stageUi++;

        int pageCount = (stageCount - 2 + stages.Count) / 36;
        if (36 * pageCount != stageCount + stages.Count) pageCount++;
        stageselOriginal[0x29E22] = checked((byte)pageCount);
        WriteOverlay(overlay, Path.Combine("data", "ui", "flash", "OTHER", "stagesel", "stagesel.gfx"), stageselOriginal);
        stageUi++;
    }

    private static void WriteOverlay(string root, string relative, byte[] data)
    {
        string path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, data);
    }
}
