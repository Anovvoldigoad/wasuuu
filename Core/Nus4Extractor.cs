using System.IO.Compression;

namespace NSC_ModManager_Android.Core;

public static class Nus4Extractor
{
        public static void Extract(string nus4Path, string destinationFolder)
    {
        if (!File.Exists(nus4Path))
            throw new FileNotFoundException("NUS4 file not found.", nus4Path);

        // --- prepare temp extraction ---
        byte[] fileData = File.ReadAllBytes(nus4Path);
        byte[] zipSignature = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        int offset = -1;
        for (int i = 0; i <= fileData.Length - zipSignature.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < zipSignature.Length; j++)
            {
                if (fileData[i + j] != zipSignature[j]) { match = false; break; }
            }
            if (match) { offset = i; break; }
        }
        if (offset < 0)
            throw new InvalidDataException("Cannot extract .nus4: embedded ZIP archive not found.");

        string tempRoot = Path.Combine(Path.GetTempPath(), "nus4_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string tempZip = Path.Combine(tempRoot, Path.GetFileNameWithoutExtension(nus4Path) + ".zip");
        string extractedTemp = Path.Combine(tempRoot, "extracted");
        Directory.CreateDirectory(extractedTemp);

        try
        {
            // write embedded ZIP to temp file and extract
            using (var outFs = new FileStream(tempZip, System.IO.FileMode.Create, FileAccess.Write, FileShare.None))
            {
                outFs.Write(fileData, offset, fileData.Length - offset);
                outFs.Flush();
            }
            System.IO.Compression.ZipFile.ExtractToDirectory(tempZip, extractedTemp);

            // --- create destination structure (do not extract into destination) ---
            Directory.CreateDirectory(destinationFolder);
            string charsRoot = Path.Combine(destinationFolder, "Characters");
            string stagesRoot = Path.Combine(destinationFolder, "Stages");
            string resourcesRoot = Path.Combine(destinationFolder, "Resources");
            string resourcesFilesData = Path.Combine(resourcesRoot, "Files", "data");
            string resourcesCpk = Path.Combine(resourcesRoot, "CPKs");
            string resourcesShaders = Path.Combine(resourcesRoot, "Shaders");

            Directory.CreateDirectory(charsRoot);
            Directory.CreateDirectory(stagesRoot);
            Directory.CreateDirectory(resourcesFilesData);
            Directory.CreateDirectory(resourcesCpk);
            Directory.CreateDirectory(resourcesShaders);

            // --- Icon, Description, Author handling ---
            string[] iconFiles = Directory.GetFiles(extractedTemp, "Icon.png", SearchOption.AllDirectories);
            if (iconFiles.Length > 0)
            {
                string srcIcon = iconFiles[0];
                string destIcon = Path.Combine(destinationFolder, "mod_icon.png");
                File.Copy(srcIcon, destIcon, true);
            }

            string description = "";
            string[] descFiles = Directory.GetFiles(extractedTemp, "Description.txt", SearchOption.AllDirectories);
            if (descFiles.Length > 0)
                description = File.ReadAllText(descFiles[0]);

            string author = "";
            string[] authorFiles = Directory.GetFiles(extractedTemp, "Author.txt", SearchOption.AllDirectories);
            if (authorFiles.Length > 0)
                author = File.ReadAllText(authorFiles[0]);

            // create mod_config.ini at destinationFolder root
            string iniPath = Path.Combine(destinationFolder, "mod_config.ini");
            var myIni = new IniFile(iniPath);
            string modName = Path.GetFileNameWithoutExtension(nus4Path);
            myIni.Write("ModName", modName, "ModManager");
            myIni.Write("Description", description, "ModManager");
            myIni.Write("Author", author, "ModManager");
            myIni.Write("LastUpdate", DateTime.Today.ToString("dd/MM/yyyy"), "ModManager");
            myIni.Write("Version", "1.0", "ModManager");
            myIni.Write("Game", "NS4", "ModManager");
            myIni.Write("EnableMod", "true", "ModManager");

            // --- copy .cpk files from any moddingapi/mods/base_game locations into Resources/CPKs ---
            var cpkFiles = Directory.GetFiles(extractedTemp, "*.cpk", SearchOption.AllDirectories);
            foreach (var cpk in cpkFiles)
            {
                // optionally ensure it's from moddingapi/mods/base_game, but copy all .cpk to be safe
                string dest = Path.Combine(resourcesCpk, Path.GetFileName(cpk));
                File.Copy(cpk, dest, true);
            }

            // --- process characters (folders that contain characode.txt) ---
            var characodeFiles = Directory.GetFiles(extractedTemp, "characode.txt", SearchOption.AllDirectories);
            foreach (var characodePath in characodeFiles)
            {
                string charFolder = Path.GetDirectoryName(characodePath);
                if (string.IsNullOrEmpty(charFolder)) continue;
                string charFolderName = new DirectoryInfo(charFolder).Name;
                string charDestRoot = Path.Combine(charsRoot, charFolderName);
                Directory.CreateDirectory(charDestRoot);

                // 1) Создаём character_config.ini
                string charIniPath = Path.Combine(charDestRoot, "character_config.ini");
                var charIni = new IniFile(charIniPath);
                charIni.Write("Partner", "false", "ModManager");
                charIni.Write("Page", "-1", "ModManager");
                charIni.Write("Slot", "-1", "ModManager");
                charIni.Write("Game", "NS4", "ModManager");
                charIni.Write("Page_NS4", "-1", "ModManager");
                charIni.Write("Slot_NS4", "-1", "ModManager");

                // 2) Определяем Partner=true если найден partnerSlotParam.xfbin в moddingapi/mods/**
                string moddingApiSrc = Path.Combine(charFolder, "moddingapi");
                bool partnerFound = false;
                if (Directory.Exists(moddingApiSrc))
                {
                    var partnerFiles = Directory.GetFiles(moddingApiSrc, "partnerSlotParam.xfbin", SearchOption.AllDirectories);
                    if (partnerFiles.Length > 0) partnerFound = true;
                }
                if (partnerFound) charIni.Write("Partner", "true", "ModManager");
                // 2.5) Копируем ВСЕ .xfbin из character/moddingapi в Characters\[Char]\moddingapi\mods\base_game
                string charModdingApiSrc = Path.Combine(charFolder, "moddingapi");
                if (Directory.Exists(charModdingApiSrc))
                {
                    string charModdingApiDest = Path.Combine(charDestRoot, "moddingapi", "mods", "base_game");
                    Directory.CreateDirectory(charModdingApiDest);

                    foreach (string xfbin in Directory.GetFiles(charModdingApiSrc, "*.xfbin", SearchOption.AllDirectories))
                    {
                        string fileName = Path.GetFileName(xfbin);
                        string destPath = Path.Combine(charModdingApiDest, fileName);

                        using (var src = new FileStream(xfbin, System.IO.FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var dst = new FileStream(destPath, System.IO.FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            src.CopyTo(dst);
                        }
                    }
                }
                // 3) Копируем ПАРАМЕТРЫ из data_win32 в Characters\[Char]\data
                string dataWin32Src = Path.Combine(charFolder, "data_win32");
                string charDataTarget = Path.Combine(charsRoot, charFolderName, "data");
                if (Directory.Exists(dataWin32Src))
                {
                    Program.CopyParamsRecursivelyModManager(dataWin32Src, charDataTarget);
                }

                // 3) Handle data_win32: copy non-param .xfbin files into Resources/Files/data using provided helper
                if (Directory.Exists(dataWin32Src))
                {
                    // Program.CopyFilesRecursivelyModManager copies only non-param .xfbin files preserving structure
                    Program.CopyFilesRecursivelyModManager(dataWin32Src, resourcesFilesData);

                    



                    

                    // --- NEW: для каждого *bod1.xfbin в папке resourcesFilesData\spc создаём копию шаблона acc ---
                    string spcFolder = Path.Combine(resourcesFilesData, "spc");
                    string appFolder = Directory.GetCurrentDirectory();
                    string accTemplate = Path.Combine(appFolder, "ParamFiles", "NS4", "1cmnbod1acc.bin.xfbin");
                    if (Directory.Exists(spcFolder) && File.Exists(accTemplate))
                    {
                        foreach (var bodFile in Directory.GetFiles(spcFolder, "*bod1.xfbin", SearchOption.TopDirectoryOnly))
                        {
                            string baseName = Path.GetFileNameWithoutExtension(bodFile); // e.g. "2nrtbod1"
                            string newName = baseName + "acc.bin.xfbin"; // e.g. "2nrtbod1acc.bin.xfbin"
                            string destPath = Path.Combine(spcFolder, newName);
                            try
                            {
                                if (baseName != "1cmnbod1")
                                    File.Copy(accTemplate, destPath, true);
                            } catch
                            {
                                // ignore individual copy errors
                            }
                        }
                    }


                }
                
                // 4) Copy shaders from charFolder/shaders into Resources/Shaders (preserve structure under shaders)
                string shadersSrc = Path.Combine(charFolder, "shaders");
                if (Directory.Exists(shadersSrc))
                {
                    CopyAllPreserveStructure(shadersSrc, resourcesShaders);
                }
            }
            IEnumerable<string> FindPrmFiles(string root)
            {
                var results = new List<string>();
                try
                {
                    var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
                    foreach (var f in Directory.EnumerateFiles(root, "*prm.bin.xfbin", options))
                        results.Add(f);
                } catch (Exception)
                {
                    // при ошибке возвращаем текущие найденные
                }
                return results;
            }
            foreach (var file in FindPrmFiles(resourcesFilesData))
            {
                Debug.WriteLine(file);
                var PRM_mod = new PRMEditorViewModel();
                PRM_mod.OpenFile(file);

                foreach (var ver in PRM_mod.VerList)
                    foreach (var sec in ver.PL_ANM_Sections)
                        foreach (var fn in sec.FunctionList)
                        {
                            if (fn.FunctionID != 0xEC) continue;
                            int orig = fn.FunctionParam1;
                            switch (orig)
                            {
                                case 1:
                                case 2:
                                case 3:
                                case 4:
                                case 9:
                                    fn.FunctionID = 0x10E;
                                    break;
                                case 5:
                                    fn.FunctionParam1 = 10;
                                    fn.FunctionParam3 = 0;
                                    break;
                                case 6:
                                    fn.FunctionParam1 = 11;
                                    fn.FunctionParam3 = 0;
                                    break;
                                case 7:
                                    fn.FunctionParam1 = 10;
                                    fn.FunctionParam3 = 1;
                                    break;
                                case 8:
                                    fn.FunctionParam1 = 11;
                                    fn.FunctionParam3 = 1;
                                    break;
                                case 10:
                                    fn.FunctionParam1 = 1;
                                    break;
                            }
                            Debug.WriteLine(orig);
                        }

                PRM_mod.SaveFile();
            }

            //Stages
            var stageFiles = Directory.GetFiles(extractedTemp, "stageMessage.txt", SearchOption.AllDirectories);
            foreach (var stagePath in stageFiles)
            {
                string stageFolder = Path.GetDirectoryName(stagePath);
                if (string.IsNullOrEmpty(stageFolder)) continue;
                string stageFolderName = new DirectoryInfo(stageFolder).Name;
                string stageDestRoot = Path.Combine(stagesRoot, stageFolderName);
                Directory.CreateDirectory(stageDestRoot);

                string bgm_id = "";
                string[] bgm_idFiles = Directory.GetFiles(extractedTemp, "BGM_ID.txt", SearchOption.AllDirectories);
                if (bgm_idFiles.Length > 0)
                    bgm_id = File.ReadAllText(bgm_idFiles[0]);

                string StageMessageID = stageFolderName + "_stageName";
                // 1) Создаём character_config.ini
                string stageIniPath = Path.Combine(stageDestRoot, "stage_config.ini");
                var stageIni = new IniFile(stageIniPath);
                stageIni.Write("BGM_ID", bgm_id, "ModManager");
                stageIni.Write("BGM_ID_NS4", bgm_id, "ModManager");
                stageIni.Write("MessageID", StageMessageID, "ModManager");
                stageIni.Write("Hell", "false", "ModManager");
                stageIni.Write("Game", "NS4", "ModManager");

                string dataWin32Src = Path.Combine(stageFolder, "data_win32");
                string stageDataTarget = Path.Combine(stagesRoot, stageFolderName, "data");
                if (Directory.Exists(dataWin32Src))
                {
                    Program.CopyParamsRecursivelyModManager(dataWin32Src, stageDataTarget);
                }
                // --- create MessageInfo from stageMessage.txt ---
                string[] stageMsgLines = File.ReadAllLines(stagePath);
                var langMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var ln in stageMsgLines)
                {
                    if (string.IsNullOrWhiteSpace(ln)) continue;
                    var parts = ln.Split(new[] { '=' }, 2);
                    if (parts.Length != 2) continue;
                    langMap[parts[0].Trim()] = parts[1].Trim();
                }

                // StageMessageID уже определён выше
                byte[] stageCrc = BinaryReader.crc32(StageMessageID);

                // Создаём MessageInfoS4 и заполняем 12 языков в порядке Program.langS4List
                MessageInfoS4ViewModel msgS4 = new MessageInfoS4ViewModel();

                // Ensure lists initialized (на случай, если конструктор не создаёт их)
                for (int i = 0; i < Program.langS4List.Length; i++)
                {
                    if (msgS4.MessageInfo_List.Count <= i)
                        msgS4.MessageInfo_List.Add(new ObservableCollection<MessageInfoModel>());
                }

                // Для каждой целевой локали берём строку из stageMessage.txt по ключу (fallback на "eng")
                for (int langIndex = 0; langIndex < Program.langS4List.Length; langIndex++)
                {
                    string langKey = Program.langS4List[langIndex]; // ожидаемые коды: arae, chi, eng, ...
                    string text = null;
                    if (langMap.ContainsKey(langKey))
                        text = langMap[langKey];
                    else if (langMap.ContainsKey("eng"))
                        text = langMap["eng"];
                    else
                        text = "";

                    var entry = new MessageInfoModel
                    {
                        CRC32Code = stageCrc,
                        MainText = Encoding.UTF8.GetBytes(text),
                        SecondaryText = Encoding.UTF8.GetBytes(text),
                        Speaker = new byte[1] {0},
                        ACBFileID = 0,
                        CueID = 0,
                        DisableText = false
                    };

                    msgS4.MessageInfo_List[langIndex].Add(entry);
                }

                // Сохраняем в папку stageDataTarget
                // SaveFileAs ожидает путь к директории с data (аналогично вашему примеру)
                msgS4.SaveFileAs(stageDataTarget);
                // 3) Handle data_win32: copy non-param .xfbin files into Resources/Files/data using provided helper
                if (Directory.Exists(dataWin32Src))
                {
                    // Program.CopyFilesRecursivelyModManager copies only non-param .xfbin files preserving structure
                    Program.CopyFilesRecursivelyModManager(dataWin32Src, resourcesFilesData);

                }

                File.Copy(
                        Path.Combine(stageFolder, "stage_tex.png"),
                        Path.Combine(stageDestRoot, "stage_preview.png"),
                        true);

                File.Copy(
                    Path.Combine(Directory.GetCurrentDirectory(), "Resources", "TemplateImages", "stage_icon.dds"),
                    Path.Combine(stageDestRoot, "stage_icon_S4.dds"),
                    true);
                File.Copy(
                    Path.Combine(Directory.GetCurrentDirectory(), "Resources", "TemplateImages", "stage_icon.dds"),
                    Path.Combine(stageDestRoot, "stage_icon_SC.dds"),
                    true);

            }
            // --- done ---
        } finally
        {
            // cleanup temp
            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            } catch
            {
                // ignore cleanup errors
            }
        }

        // helper local functions
        static void CopyOnlyXfbinPreserveStructure(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(sourceDir)) return;
            foreach (string src in Directory.GetFiles(sourceDir, "*.xfbin", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(sourceDir, src);
                string dest = Path.Combine(targetDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(src, dest, true);
            }
        }

        static void CopyAllPreserveStructure(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(sourceDir)) return;
            foreach (string src in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(sourceDir, src);
                string dest = Path.Combine(targetDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(src, dest, true);
            }
        }

        static string NormalizePath(string path) => path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
    }
}
